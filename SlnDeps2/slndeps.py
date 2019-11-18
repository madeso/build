import argparse
import logic

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description='Generate a solution dependency file')
    # parser.add_argument('cmd', help='the command to execute')
    parser.add_argument('solution', help='the solution')

    parser.add_argument('--target', help='the target')
    parser.add_argument('--format', help='the format')
    parser.add_argument('--exclude', help='projects to exclude', nargs='*')
    parser.add_argument('--simplify', dest='simplify', action='store_const', const=True, default=False, help='simplify output')
    parser.add_argument('--style', help='the style')
    parser.add_argument('--reverse', dest='reverse', action='store_const', const=True, default=False, help='reverse arrows')

    args = parser.parse_args()
    logic.toGraphviz(args.solution, args.target, args.format, args.exclude, args.simplify, args.style, args.reverse)
# using System;
# using System.Collections.Generic;
# using System.ComponentModel;
# using System.Data;
# using System.Drawing;
# using System.Linq;
# using System.Text;
# using System.Windows.Forms;
# using System.IO;
# using System.Xml;
#
# namespace SlnDeps
# {
# 	public partial class Main : Form
# 	{
# 		public Main()
# 		{
# 			InitializeComponent();
# 		}
#
# 		private void dBrowseSource_Click(object sender, EventArgs e)
# 		{
# 			if (ofd.ShowDialog() != DialogResult.OK) return;
# 			dSource.Text = ofd.FileName;
# 		}
#
# 		private void dBrowseTarget_Click(object sender, EventArgs e)
# 		{
# 			if (sfd.ShowDialog() != DialogResult.OK) return;
# 			dTarget.Text = sfd.FileName;
# 		}
#
# 		private void dGo_Click(object sender, EventArgs e)
# 		{
# 			saveSettings();
# 			List<string> items = new List<string>();
# 			foreach (var c in dExcludes.CheckedItems)
# 			{
# 				items.Add(c.ToString());
# 			}
# 			if (items.Count == 0) items = null;
# 			Logic.toGraphviz(dSource.Text, dTarget.Text, dFormat.Text, items, dSimplifyLinks.Checked, dStyle.Text, dReverseArrows.Checked);
# 		}
#
# 		private void dFillExcludes_Click(object sender, EventArgs e)
# 		{
# 			if (dExcludes.Items.Count != 0)
# 			{
# 				if (MessageBox.Show("Items is not empty, continue?", "Continue?", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
# 			}
#
# 			dExcludes.Items.Clear();
# 			Dictionary<string, string> settings = LoadSettings(ConfigurationFile);
#
# 			foreach (var s in Logic.GetProjects(dSource.Text))
# 			{
# 				dExcludes.Items.Add(s, GetBool(settings, s, true));
# 			}
# 		}
#
# 		void saveSettings()
# 		{
# 			Dictionary<string, string> settings = new Dictionary<string, string>();
# 			for (int i = 0; i < dExcludes.Items.Count; ++i )
# 			{
# 				bool ch = dExcludes.CheckedIndices.Contains(i);
# 				string name = dExcludes.Items[i].ToString();
# 				settings[name] = ch.ToString();
# 			}
#
# 			SaveSettings(ConfigurationFile, settings);
# 		}
#
# 		private string ConfigurationFile
# 		{
# 			get
# 			{
# 				return Path.ChangeExtension(dSource.Text, "slndeps");
# 			}
# 		}
#
# 		bool GetBool(Dictionary<string, string> dict, string name, bool def)
# 		{
# 			if (dict.ContainsKey(name))
# 			{
# 				return bool.Parse(dict[name]);
# 			}
# 			else return def;
# 		}
#
# 		private Dictionary<string, string> LoadSettings(string file)
# 		{
# 			Dictionary<string, string> dict = new Dictionary<string,string>();
# 			if (false == File.Exists(file)) return dict;
# 			var lines = File.ReadAllLines(file);
# 			foreach (var ol in lines)
# 			{
# 				var l = ol.Trim();
# 				if (l.StartsWith("#")) continue;
# 				var sp = l.Split("=".ToCharArray(), 2);
# 				if (sp.Length != 2) continue;
# 				var k = sp[0].Trim();
# 				var v = sp[1].Trim();
# 				dict[k] = v;
# 			}
#
# 			return dict;
# 		}
#
# 		private void SaveSettings(string file, Dictionary<string, string> dict)
# 		{
# 			List<string> lines = new List<string>();
# 			foreach (var e in dict)
# 			{
# 				lines.Add( string.Format("{0}={1}", e.Key, e.Value) );
# 			}
# 			File.WriteAllLines(file, lines.ToArray());
# 		}