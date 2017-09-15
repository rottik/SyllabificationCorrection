using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace SyllabificationCorrection
{
    public partial class Form1 : Form
    {
        private List<Slovo> words;
        List<Slovo> OKwords = new List<Slovo>();
        List<Slovo> correctedWords = new List<Slovo>();
        List<Slovo> abbrevList = new List<Slovo>();
        List<Slovo> ForeingList = new List<Slovo>();
        Slovo showedWord;
        string dataFile;

        public Slovo ShowedWord
        {
            get { return showedWord; }
            set { showedWord = value; }
        }
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            openFileDialog1.Multiselect = false;
            openFileDialog1.Filter = "PCS Files (.pcs)|*.pcs|JSON Files (.json)|*.json|All Files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                dataFile = openFileDialog1.FileName;
                if (dataFile.EndsWith(".json"))
                    words = ParseJson(dataFile);
                if (dataFile.EndsWith(".pcs"))
                    words = ParsePCS(dataFile);

                //SaveToPCS(dataFile.Replace("json", "pcs"));

                if (MessageBox.Show("Load previous work?", "Load previous work?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    if (File.Exists(dataFile + ".okWords"))
                        OKwords = ParsePCS(dataFile + ".okWords");
                    if (File.Exists(dataFile + ".correctedWords"))
                        correctedWords = ParsePCS(dataFile + ".correctedWords");
                    if (File.Exists(dataFile + ".foreignWords"))
                        ForeingList = ParsePCS(dataFile + ".foreignWords");
                    if (File.Exists(dataFile + ".abbrevationWords"))
                        abbrevList = ParsePCS(dataFile + ".abbrevationWords");
                    //MessageBox.Show("Not working!");

                    List<Slovo> remove = OKwords.Union(correctedWords).Union(ForeingList).Union(abbrevList).ToList();
                    words = words.Where(p=>!remove.Contains(p)).ToList(); ;
                }
                showedWord = words.First();
                ShowWord(showedWord);
            }
            else
                this.Close();

        }

        private void SaveToPCS(string fileName)
        {
            SaveToPCS(fileName, this.words);
        }

        private void SaveToPCS(string fileName, List<Slovo> words)
        {
            if (words.Count == 0)
                return;
            List<Slovo> empty = new List<Slovo>();
            TextWriter tw = new StreamWriter(fileName);
            tw.WriteLine("==========");
            foreach (Slovo word in words)
            {
                try
                {
                    tw.WriteLine(word.Ortho + "\t" + word.Phone.Keys.First());
                    foreach (string s in word.Phone[word.Phone.Keys.First()])
                        tw.WriteLine("\t" + s);
                    tw.WriteLine("==========");
                }
                catch (Exception e)
                {
                    empty.Add(word);
                }
            }
            tw.Close();
            if(empty.Count>0)
                File.WriteAllLines(fileName + ".errors", empty.Select(p => p.Ortho));
        }

        private List<Slovo> ParsePCS(string file)
        {
            string[] wordParts = File.ReadAllText(file).Trim().Split(new string[] { "==========" }, StringSplitOptions.RemoveEmptyEntries);
            List<Slovo> ws = new List<Slovo>();
            Progress progresForm = new Progress();
            progresForm.Show();
            int cnt = 0;
            int step = wordParts.Length / progresForm.progressBar1.Maximum;
            if (step == 0) step = 1;
            foreach (string part in wordParts)
            {
                string ortho = "";
                string ortSyl = "";
                Dictionary<string, List<string>> phns = new Dictionary<string, List<string>>();
                foreach (string line in part.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!line.StartsWith("\t"))
                    {
                        ortho = line.Split('\t')[0];
                        ortSyl = line.Split('\t')[1];
                        phns.Add(ortSyl, new List<string>());
                    }
                    else
                        phns[ortSyl].Add(line.Trim());
                }

                Slovo s = new Slovo(ortho, phns);
                ws.Add(s);
                cnt++;
                if (cnt % step == 0)
                {
                    progresForm.progressBar1.Value++;
                }
            }
            progresForm.Close();
            return ws;
        }



        private void ShowWord(Slovo w)
        {
            treeView1.Nodes.Clear();
            foreach (string key in w.Phone.Keys)
            {
                label1.Text = w.Ortho;
                string ortSyl = key;

                List<TreeNode> tnList = new List<TreeNode>();
                foreach (string phn in w.Phone[ortSyl])
                {
                    TreeNode tnp = new TreeNode("phn: " + phn);
                    tnList.Add(tnp);
                }

                TreeNode n = new TreeNode("ort: " + key, tnList.ToArray());
                n.ExpandAll();

                treeView1.Nodes.Add(n);
            }
        }

        private static List<Slovo> ParseJson(string fileJson)
        {
            List<Slovo> slova = new List<Slovo>();
            string content = Regex.Unescape(File.ReadAllText(fileJson)).Substring(1);
            string[] parts = content.Split(new string[] { @"}," }, StringSplitOptions.RemoveEmptyEntries);
            Regex ortho = new Regex("\"([\\p{L}\\p{N}]+)\"\\:\\s\\{");
            Regex phone = new Regex("([\\p{L}\\p{N}]+)\":\\s\\[(\\[.*?\\])\\]");
            Regex syllabsReg = new Regex("\"([\\p{L}\\p{N}]+)\",\\s+\"([\\p{L}\\p{N}]+)\"");
            Progress progresForm = new Progress();
            progresForm.Show();
            int cnt = 0;
            int step = parts.Length / 100;
            if (step == 0) step = 1;
            foreach (string line in parts)
            {
                cnt++;
                Match m = ortho.Match(line);
                string lineTmp = line.Substring(m.Index + m.Length);
                MatchCollection mPHs = phone.Matches(lineTmp);
                Dictionary<string, List<string>> dic = new Dictionary<string, List<string>>();
                foreach (Match mc in mPHs)
                {
                    Console.WriteLine();
                    string syls = mc.Groups[2].ToString();
                    MatchCollection mc2 = syllabsReg.Matches(syls);
                    List<Tuple<string, string>> sylabs = new List<Tuple<string, string>>();
                    foreach (Match mxx in mc2)
                    {
                        Tuple<string, string> tp = new Tuple<string, string>(mxx.Groups[1].ToString(), mxx.Groups[2].ToString());
                        sylabs.Add(tp);
                    }

                    StringBuilder sbORTSyl = new StringBuilder();
                    foreach (string s in sylabs.Select(p => p.Item1))
                        sbORTSyl.Append(s + "-");
                    string ortSyl = sbORTSyl.Remove(sbORTSyl.Length - 1, 1).ToString();
                    if (!dic.ContainsKey(ortSyl))
                        dic.Add(ortSyl, new List<string>());

                    StringBuilder sbPHNSyl = new StringBuilder();
                    foreach (string s in sylabs.Select(p => p.Item2))
                        sbPHNSyl.Append(s + "-");
                    string phnSyl = sbPHNSyl.Remove(sbPHNSyl.Length - 1, 1).ToString();
                    dic[ortSyl].Add(phnSyl);

                    //dic.Add(mc.Groups[1].ToString(), sylabs);
                }
                Slovo w = new Slovo(m.Groups[1].ToString(), dic);
                slova.Add(w);
                if (cnt % step == 0)
                {
                    progresForm.progressBar1.Value++;
                }
            }
            progresForm.Close();
            return slova; ;
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            if (treeView1.Nodes.Count == 0) return;
            OKwords.Add(showedWord);
            LoadNewWord();
        }

        private void LoadNewWord()
        {
            List<Slovo> remove = OKwords.Union(correctedWords).Union(ForeingList).Union(abbrevList).ToList();
            words = words.Except(remove).ToList();
            if (words.Count != 0)
            {
                showedWord = words.First();
                ShowWord(showedWord);
            }
            else
            {
                MessageBox.Show("Nothing to show!");
                treeView1.Nodes.Clear();
            }
        }

        private void buttonFix_Click(object sender, EventArgs e)
        {
            if (treeView1.Nodes.Count == 0) return;
            Dictionary<string, List<string>> dic = new Dictionary<string, List<string>>();
            foreach (TreeNode node in treeView1.Nodes)
            {
                List<string> phnList = new List<string>();
                foreach (TreeNode PHNnd in node.Nodes)
                {
                    phnList.Add(PHNnd.Text.Replace("phn: ",""));
                }
                dic.Add(node.Text.Replace("ort: ",""), phnList);
            }
            showedWord.Phone = dic;
            correctedWords.Add(showedWord);
            LoadNewWord();
        }

        private bool ort = false;
        private bool phn = false;
        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            ort = false;
            phn = false;
            if (treeView1.SelectedNode.Text.Contains("ort: "))
                ort = true;
            if (treeView1.SelectedNode.Text.Contains("phn: "))
                phn = true;
            textBox1.Text = treeView1.SelectedNode.Text.Replace("ort: ", "").Replace("phn: ", "");
        }

        private void buttonChange_Click(object sender, EventArgs e)
        {
            string prefix = "";
            if (ort) prefix = "ort: ";
            if (phn) prefix = "phn: ";
            treeView1.SelectedNode.Text = prefix + textBox1.Text.Trim();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult res = MessageBox.Show("Save", "Save", MessageBoxButtons.YesNoCancel);
            if (res == DialogResult.Yes)
            {
                SaveToPCS(dataFile + ".okWords", OKwords);
                SaveToPCS(dataFile + ".correctedWords", correctedWords);
                SaveToPCS(dataFile + ".foreignWords", ForeingList);
                SaveToPCS(dataFile + ".abbrevationWords", abbrevList);
            }
            else if (res == DialogResult.Cancel)
            {
                e.Cancel = true;
            }
            else
            {
                return;
            }
        }

        private void buttonForeign_Click(object sender, EventArgs e)
        {
            if (treeView1.Nodes.Count == 0) return;
            ForeingList.Add(showedWord);
            LoadNewWord();
        }

        private void buttonAbbrevation_Click(object sender, EventArgs e)
        {
            if (treeView1.Nodes.Count == 0) return;
            abbrevList.Add(showedWord);
            LoadNewWord();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SaveToPCS(dataFile + ".okWords", OKwords);
            SaveToPCS(dataFile + ".correctedWords", correctedWords);
            SaveToPCS(dataFile + ".foreignWords", ForeingList);
            SaveToPCS(dataFile + ".abbrevationWords", abbrevList);
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }

    public class Slovo
    {
        string ortho;

        public override bool Equals(Object obj)
        {
            Slovo other = obj as Slovo;
            if (other == null) return false;
            if (other.Ortho == this.Ortho)
                return true;
            return false;
        }


        public string Ortho
        {
            get { return ortho; }
        }
        Dictionary<string, List<string>> phone = new Dictionary<string, List<string>>();

        public Dictionary<string, List<string>> Phone
        {
            get { return phone; }
            set { phone = value; }
        }

        public Slovo(string _ortho, Dictionary<string, List<string>> _phone)
        {
            ortho = _ortho;
            phone = _phone;
        }

    }
}
