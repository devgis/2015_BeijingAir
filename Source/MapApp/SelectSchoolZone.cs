using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MapApp
{
    public partial class SelectSchoolZone : Form
    {
        public List<string> listItem = null;
        public string SelectItem = string.Empty;
        public SelectSchoolZone()
        {
            InitializeComponent();
        }

        private void SelectSchoolZone_Load(object sender, EventArgs e)
        {
            if (listItem != null)
            {
                foreach (string s in listItem)
                {
                    comboBox1.Items.Add(s);
                }
            }
        }

        private void btSelect_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(comboBox1.Text))
            {
                SelectItem = comboBox1.Text;
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                MessageBox.Show("请选择!");
            }
        }

        private void btCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
