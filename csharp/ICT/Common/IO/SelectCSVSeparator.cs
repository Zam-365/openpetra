﻿//
// DO NOT REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
//
// @Authors:
//       timop
//
// Copyright 2004-2010 by OM International
//
// This file is part of OpenPetra.org.
//
// OpenPetra.org is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// OpenPetra.org is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with OpenPetra.org.  If not, see <http://www.gnu.org/licenses/>.
//
using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Data;

namespace Ict.Common.IO
{
    /// <summary>
    /// Description of SelectCSVSeparator.
    /// </summary>
    public partial class TDlgSelectCSVSeparator : Form
    {
        private string FSeparator;
        private List <String>FCSVRows = null;

        /// <summary>
        /// read the separator that the user has selected
        /// </summary>
        public string SelectedSeparator
        {
            get
            {
                return FSeparator;
            }
        }

        /// <summary>
        /// constructor
        /// </summary>
        public TDlgSelectCSVSeparator()
        {
            //
            // The InitializeComponent() call is required for Windows Forms designer support.
            //
            InitializeComponent();

            FSeparator = TAppSettingsManager.GetValueStatic("CSVSeparator",
                System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator);

            if (FSeparator == ";")
            {
                rbtSemicolon.Checked = true;
            }
            else if (FSeparator == ",")
            {
                rbtComma.Checked = true;
            }
            else if (FSeparator == "\t")
            {
                rbtTabulator.Checked = true;
            }
            else
            {
                rbtOther.Checked = true;
                txtOtherSeparator.Text = FSeparator;
            }
        }

        /// <summary>
        /// set the filename for CSV
        /// </summary>
        public string CSVFileName
        {
            set
            {
                StreamReader reader = new StreamReader(value, TTextFile.GetFileEncoding(value), false);

                FCSVRows = new List <string>();

                while (!reader.EndOfStream && FCSVRows.Count < 6)
                {
                    FCSVRows.Add(reader.ReadLine());
                }

                RbtCheckedChanged(null, null);
            }
        }

        void RbtCheckedChanged(object sender, EventArgs e)
        {
            txtOtherSeparator.Enabled = rbtOther.Checked;

            if (rbtComma.Checked)
            {
                FSeparator = ",";
            }
            else if (rbtSemicolon.Checked)
            {
                FSeparator = ";";
            }
            else if (rbtTabulator.Checked)
            {
                FSeparator = "\t";
            }
            else if (rbtOther.Checked)
            {
                FSeparator = txtOtherSeparator.Text;
            }

            if ((FSeparator.Length > 0) && (FCSVRows != null))
            {
                DataTable table = new DataTable();
                string line = FCSVRows[0];

                while (line.Length > 0)
                {
                    string header = StringHelper.GetNextCSV(ref line, FSeparator);

                    if (header.StartsWith("#"))
                    {
                        header = header.Substring(1);
                    }

                    table.Columns.Add(header);
                }

                for (int counter = 1; counter < FCSVRows.Count; counter++)
                {
                    line = FCSVRows[counter];
                    DataRow row = table.NewRow();
                    int countColumns = 0;

                    while (line.Length > 0 && countColumns < table.Columns.Count)
                    {
                        row[countColumns] = StringHelper.GetNextCSV(ref line, FSeparator);
                        countColumns++;
                    }

                    table.Rows.Add(row);
                }

                table.DefaultView.AllowNew = false;
                table.DefaultView.AllowDelete = false;
                table.DefaultView.AllowEdit = false;
                grdPreview.DataSource = table;
            }
        }
    }
}