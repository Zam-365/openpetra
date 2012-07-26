//
// DO NOT REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
//
// @Authors:
//       timop
//
// Copyright 2004-2012 by OM International
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
using System.Collections.Generic;
using System.Data;
using System.IO;
using System;
using System.Collections;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Text;

using Ext.Net;
using Ict.Common;
using Ict.Common.IO;
using Ict.Common.Verification;
using PetraWebService;
using Ict.Petra.Shared;
using Ict.Petra.Server.MConference.Applications;
using Ict.Petra.Shared.MPartner.Mailroom.Data;

namespace Ict.Petra.WebServer.MConference
{
    public partial class THeadsetManagementUI : System.Web.UI.Page
    {
        protected Ext.Net.TextArea PartnerKeysHandingOut;
        protected Ext.Net.TextArea PartnerKeysReturning;
        protected Ext.Net.FormPanel HeadsetsForm;
        protected Ext.Net.ComboBox CurrentSessionHandingOut;
        protected Ext.Net.ComboBox CurrentSessionReturning;
        protected Ext.Net.Store SessionsStore;

        protected void Page_Load(object sender, EventArgs e)
        {
            // check for valid user
            TOpenPetraOrg myServer = new TOpenPetraOrg();

            if (!myServer.IsUserLoggedIn())
            {
                this.Response.Redirect("Default.aspx");
                return;
            }

            if (!X.IsAjaxRequest)
            {
                Sessions_Refresh(null, null);
            }
        }

        protected void Sessions_Refresh(object sender, StoreRefreshDataEventArgs e)
        {
            PContactAttributeDetailTable sessions = THeadsetManagement.GetSessions();

            ArrayList Result = new ArrayList();

            sessions.DefaultView.Sort = PContactAttributeDetailTable.GetDateCreatedDBName() + " DESC";

            foreach (DataRowView rv in sessions.DefaultView)
            {
                PContactAttributeDetailRow row = (PContactAttributeDetailRow)rv.Row;

                object[] NewRow = new object[1];

                NewRow[0] = row.ContactAttrDetailCode;

                Result.Add(NewRow);
            }

            this.SessionsStore.DataSource = Result.ToArray();

            this.SessionsStore.DataBind();
        }

        protected void AddNewSession(Object sender, DirectEventArgs e)
        {
            Dictionary <string, string>values = JSON.Deserialize <Dictionary <string, string>>(e.ExtraParams["Values"]);
            THeadsetManagement.AddSession(values["txtNewSession"].ToString());
            Sessions_Refresh(null, null);
            CurrentSessionHandingOut.SelectedIndex = 0;
            CurrentSessionReturning.SelectedIndex = 0;
        }

        protected void ImportHeadsetKeysHandingOut(Object sender, DirectEventArgs e)
        {
            Dictionary <string, string>values = JSON.Deserialize <Dictionary <string, string>>(e.ExtraParams["Values"]);
            string EnteredValues = values["PartnerKeysHandingOut"].ToString().Trim();

            if (THeadsetManagement.AddScannedKeys(values["CurrentSessionHandingOut"].ToString(), EnteredValues, true))
            {
                PartnerKeysHandingOut.Clear();
            }
        }

        protected void ImportHeadsetKeysReturning(Object sender, DirectEventArgs e)
        {
            Dictionary <string, string>values = JSON.Deserialize <Dictionary <string, string>>(e.ExtraParams["Values"]);
            string EnteredValues = values["PartnerKeysReturning"].ToString().Trim();

            if (THeadsetManagement.AddScannedKeys(values["CurrentSessionReturning"].ToString(), EnteredValues, false))
            {
                PartnerKeysReturning.Clear();
            }
        }

        protected void ReportUnreturnedHeadsets(Object sender, DirectEventArgs e)
        {
            this.Response.Clear();
            this.Response.ContentType = "application/xlsx";
            this.Response.AddHeader("Content-Type", "application/xlsx");
            this.Response.AddHeader("Content-Disposition", "attachment; filename=UnreturnedHeadsets.xlsx");
            MemoryStream m = new MemoryStream();
            THeadsetManagement.DownloadUnreturnedHeadsets(m);
            m.WriteTo(this.Response.OutputStream);
            m.Close();
            this.Response.End();
        }

        protected void ReportStatistics(Object sender, DirectEventArgs e)
        {
        }
    }
}