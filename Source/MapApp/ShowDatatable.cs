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
    public partial class ShowDatatable : Form
    {
        DataTable dt = null;
        public ShowDatatable(DataTable dtResult)
        {
            InitializeComponent();
            dt = dtResult;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            dataGridView1.DataSource = dt;
            //dataGridView1.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
        }
    }
}
