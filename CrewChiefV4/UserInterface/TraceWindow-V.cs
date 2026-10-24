using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Reflection;
using Newtonsoft.Json;

namespace CrewChiefV4
{
    public class TraceWindow : Form
    {
        private TableLayoutPanel tableLayoutPanel;
        private TreeView treeView;
        private Button exceptionButton;
        private TextBox logTextBox;
        private const string traceNodesFile = "traceNodes.json";
        private StringWriter consoleOutput;

        public TraceWindow()
        {
            InitializeComponent();

            if (DarkModeForms.DarkModeCS.IsDarkModeCSEnabled)
                _ = new DarkModeForms.DarkModeCS(this);

            PopulateTreeView();
            LoadNodeStates();
            if (UnitTest.UnitTest.Active)
            {
                logTextBox.Text = "Hello, Trace Window!\r\n";
                RedirectConsoleOutput();
            }
        }

        private void InitializeComponent()
        {
            this.tableLayoutPanel = new TableLayoutPanel();
            this.treeView = new TreeView();
            this.exceptionButton = new Button();
            this.logTextBox = new TextBox();

            // 
            // tableLayoutPanel
            // 
            this.tableLayoutPanel.ColumnCount = 1;
            this.tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.tableLayoutPanel.Dock = DockStyle.Fill;
            this.tableLayoutPanel.RowCount = 3;
            this.tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 70F));
            this.tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            this.tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));
            this.tableLayoutPanel.Controls.Add(this.treeView, 0, 0);
            this.tableLayoutPanel.Controls.Add(this.exceptionButton, 0, 1);
            this.tableLayoutPanel.Controls.Add(this.logTextBox, 0, 2);

            // 
            // treeView
            // 
            this.treeView.Dock = DockStyle.Fill;
            this.treeView.AfterSelect += new TreeViewEventHandler(TreeView_AfterSelect);
            this.treeView.AfterCheck += new TreeViewEventHandler(TreeView_AfterCheck);
            this.treeView.CheckBoxes = true; // Enable checkboxes

            if (UnitTest.UnitTest.Active)
            {
                // 
                // exceptionButton
                // 
                this.exceptionButton.Text = "Exception";
                this.exceptionButton.Dock = DockStyle.Fill;
                this.exceptionButton.Click += new EventHandler(ExceptionButton_Click);

                // 
                // logTextBox
                // 
                this.logTextBox.Dock = DockStyle.Fill;
                this.logTextBox.Multiline = true;
                this.logTextBox.ReadOnly = true;
                this.logTextBox.ScrollBars = ScrollBars.Vertical;
            }

            // 
            // TraceWindow
            // 
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Controls.Add(this.tableLayoutPanel);
            this.Text = "Trace Window";
            this.FormClosing += new FormClosingEventHandler(TraceWindow_FormClosing);
        }

        private void PopulateTreeView()
        {
            TreeNode rootNode = new TreeNode("Tracepoints");
            Type breakpointsType = typeof(Tracepoints);

            foreach (Type nestedType in breakpointsType.GetNestedTypes(BindingFlags.Public))
            {
                TreeNode nestedTypeNode = new TreeNode(nestedType.Name);
                foreach (MethodInfo method in nestedType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    nestedTypeNode.Nodes.Add(new TreeNode(method.Name));
                }
                rootNode.Nodes.Add(nestedTypeNode);
            }

            treeView.Nodes.Add(rootNode);
            treeView.ExpandAll();

            // Set initial state of nodes
            SetNodeState(rootNode, rootNode.Checked);
        }

        private void TreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            // Handle the user interaction here
            // MessageBox.Show($"Selected Node: {e.Node.Text}");
        }

        private void TreeView_AfterCheck(object sender, TreeViewEventArgs e)
        {
            // Prevent recursive calls
            treeView.AfterCheck -= TreeView_AfterCheck;

            // Gray out or enable child nodes based on the checked state of the parent node
            SetNodeState(e.Node, e.Node.Checked);

            // Update parent node check state
            UpdateParentNodeCheckState(e.Node);

            // Reattach the event handler
            treeView.AfterCheck += TreeView_AfterCheck;
        }

        private void SetNodeState(TreeNode node, bool isChecked)
        {
            foreach (TreeNode childNode in node.Nodes)
            {
                childNode.ForeColor = isChecked ? SystemColors.WindowText : SystemColors.GrayText;
                SetNodeState(childNode, isChecked); // Recursively set the state based on the parent's checked state
            }
        }

        private void UpdateParentNodeCheckState(TreeNode node)
        {
            if (node.Parent != null)
            {
                bool anyChildChecked = false;
                foreach (TreeNode sibling in node.Parent.Nodes)
                {
                    if (sibling.Checked)
                    {
                        anyChildChecked = true;
                        break;
                    }
                }

                node.Parent.Checked = anyChildChecked;
                node.Parent.ForeColor = anyChildChecked ? SystemColors.WindowText : SystemColors.GrayText;

                // Recursively update the parent node
                UpdateParentNodeCheckState(node.Parent);
            }
        }

        private void SaveNodeStates()
        {
            var nodeStates = new Dictionary<string, bool>();
            SaveNodeState(treeView.Nodes, nodeStates);

            string json = JsonConvert.SerializeObject(nodeStates, Formatting.Indented);
            File.WriteAllText(traceNodesFile, json);
        }

        private void SaveNodeState(TreeNodeCollection nodes, Dictionary<string, bool> nodeStates, string parentPath = "")
        {
            foreach (TreeNode node in nodes)
            {
                string path = string.IsNullOrEmpty(parentPath) ? node.Text : $"{parentPath}/{node.Text}";
                nodeStates[path] = node.Checked;
                SaveNodeState(node.Nodes, nodeStates, path);
            }
        }

        private void LoadNodeStates()
        {
            if (!File.Exists(traceNodesFile))
                return;

            var nodeStates = JsonConvert.DeserializeObject<Dictionary<string, bool>>(File.ReadAllText(traceNodesFile));
            LoadNodeState(treeView.Nodes, nodeStates);
        }

        private void LoadNodeState(TreeNodeCollection nodes, Dictionary<string, bool> nodeStates, string parentPath = "")
        {
            foreach (TreeNode node in nodes)
            {
                string path = string.IsNullOrEmpty(parentPath) ? node.Text : $"{parentPath}/{node.Text}";
                if (nodeStates.TryGetValue(path, out bool isChecked))
                {
                    node.Checked = isChecked;
                    node.ForeColor = isChecked ? SystemColors.WindowText : SystemColors.GrayText;
                }
                LoadNodeState(node.Nodes, nodeStates, path);
            }
        }

        private void TraceWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveNodeStates();
        }

        public bool IsNodeChecked(string nodePath)
        {
            bool nodeExists = NodeExists(treeView.Nodes, nodePath);
            Debug.Assert(nodeExists, $"Node path '{nodePath}' not found in the tree.");
            return nodeExists && IsNodeChecked(treeView.Nodes, nodePath);
        }

        private bool NodeExists(TreeNodeCollection nodes, string nodePath, string parentPath = "")
        {
            foreach (TreeNode node in nodes)
            {
                string path = string.IsNullOrEmpty(parentPath) ? node.Text : $"{parentPath}/{node.Text}";
                if (path == nodePath)
                {
                    return true;
                }
                if (NodeExists(node.Nodes, nodePath, path))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsNodeChecked(TreeNodeCollection nodes, string nodePath, string parentPath = "")
        {
            foreach (TreeNode node in nodes)
            {
                string path = string.IsNullOrEmpty(parentPath) ? node.Text : $"{parentPath}/{node.Text}";
                if (path == nodePath)
                {
                    return node.Checked;
                }
                bool isChecked = IsNodeChecked(node.Nodes, nodePath, path);
                if (isChecked)
                {
                    return true;
                }
            }
            return false;
        }

        #region Unit testing
        private void ExceptionButton_Click(object sender, EventArgs e)
        {
            try
            {
                throw new Exception("Test exception");
            }
            catch (Exception ex)
            {
                Tracepoints.Debugging.LogException(ex);
                UpdateLogTextBox();
            }
        }

        private void UpdateLogTextBox()
        {
            // Update the logTextBox with the captured console output
            logTextBox.Text += consoleOutput.ToString();
        }

        private void RedirectConsoleOutput()
        {
            consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);
            Console.SetError(consoleOutput);
        }
        #endregion Unit testing
    }
}
