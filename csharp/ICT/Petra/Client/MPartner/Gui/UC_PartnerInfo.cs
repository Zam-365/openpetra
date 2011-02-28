//
// DO NOT REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
//
// @Authors:
//       christiank
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
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using GNU.Gettext;

using Ict.Common;
using Ict.Petra.Client.App.Core;
using Ict.Petra.Client.App.Gui;
using Ict.Petra.Client.CommonForms;
using Ict.Petra.Shared;
using Ict.Petra.Shared.MCommon.Data;
using Ict.Petra.Shared.MPartner;
using Ict.Petra.Shared.MPartner.Mailroom.Data;
using Ict.Petra.Shared.MPartner.Partner.Data;
using Ict.Petra.Shared.RemotedExceptions;

namespace Ict.Petra.Client.MPartner.Gui
{
    interface IPartnerInfoMethods
    {
        void ClearControls();
        void PassPartnerDataPartialWithLocation(Int64 APartnerKey, DataRow APartnerDR);
        void PassPartnerDataPartialWithoutLocation(Int64 APartnerKey, DataRow APartnerDR);
        void PassPartnerDataFull(Int64 APartnerKey, DataRow APartnerDR);
    }

    /// <summary>
    /// UserControl for displaying Partner Info data.
    /// </summary>
    /// <remarks>
    /// Uses a Timer that ensures that the server calls are not made in too close intervals
    /// (to preserve server resources) and that makes the server-side call run on its
    /// own Thread and through that keeps the GUI responsive.
    /// </remarks>
    public partial class TUC_PartnerInfo : TPetraUserControl, IPartnerInfoMethods
    {
        delegate void TDataFetcher (object ADummy);
        delegate void TMyUpdateDelegate();

        /// <summary>Minimum gap between Server Calls in Milliseconds</summary>
        private const Int16 MINIMUM_SERVERCALL_GAP = 1500;

        private Int64 FPartnerKey;
        private Int64 FLastPartnerKey;
        private TPartnerClass FPartnerClass;
        private string FPartnerShortName;
        private DataRow FPartnerDR;
        private PartnerInfoTDS FPartnerInfoDS;
        private bool FLocationDataServerRetrieved;
        private bool FHeadDataServerRetrieved;

        /// <summary>True is the server is fetching Partner Info data at the moment,
        /// otherwise false.</summary>
        private bool FFetchingData = false;

        /// <summary>Stores the time when the server last fetched Partner Info data.</summary>
        private DateTime FLastDataFeched = DateTime.MinValue;

        /// <summary>
        /// Timer that ensures that the server calls are not made in too close intervals
        /// (to preserve server resources) and that makes the server-side call run on its
        /// own Thread and through that keeps the GUI responsive.
        /// </summary>
        private System.Threading.Timer FTimer;

        /// <summary>
        /// Holds a reference to a Delegate Method that is executed when the Timer fires.
        /// </summary>
        private Ict.Petra.Client.MPartner.Gui.TUC_PartnerInfo.TDataFetcher FDataFetcher;

        /// <summary>Holds Parameters for a requested Server-side call for retrieval of Partner
        /// Info data.</summary>
        private Ict.Petra.Client.MPartner.Gui.TUC_PartnerInfo.TServerCallParams FServerCallParams;

        /// <summary>Holds Parameters for the curent Server-side call for retrieval of Partner
        /// Info data.</summary>
        private Ict.Petra.Client.MPartner.Gui.TUC_PartnerInfo.TServerCallParams FCurrentServerCallParams;

        /// <summary>Lock object that is used for thread-save access to <see cref="FServerCallParams" />.</summary>
        private static object FServerCallParamsLock;

        private string FServerCallError = "";


        /// <summary>Scope of data that is already available client-side.</summary>
        public enum TPartnerInfoAvailDataEnum
        {
            /// <summary>Partner 'head' data and Location data only</summary>
            piadHeadAndLocationOnly,

            /// <summary>Partner 'head' data only</summary>
            piadHeadOnly,

            /// <summary>All PartnerInfo data</summary>
            piadFull,

            /// <summary>No PartnerInfo data</summary>
            piadNone
        }

        /// <summary>
        /// Holds Parameters for a Server-side call for retrieval of Partner
        /// Info data.
        /// </summary>
        /// <remarks>Two Methods, <see cref="GetDeepCopy" /> and
        /// <see cref="IsIdentical(TServerCallParams)" />
        /// provide functionality beyond just storing data.</remarks>
        private class TServerCallParams
        {
            Int64 FPartnerKey;

            TLocationPK FLocationKey;

            TPartnerInfoScopeEnum FPartnerInfoScope;

            TPartnerInfoAvailDataEnum FScope;

            bool FLoadRestOfData;

            bool FServerCallOK;


            /// <summary>PartnerKey of Partner that should be retrieved.</summary>
            public long PartnerKey
            {
                get
                {
                    return FPartnerKey;
                }
                set
                {
                    FPartnerKey = value;
                }
            }

            /// <summary>Location Primary Key of Partner that should be retrieved.</summary>
            public TLocationPK LocationKey
            {
                get
                {
                    return FLocationKey;
                }
                set
                {
                    FLocationKey = value;
                }
            }


            /// <summary>Scope of data that should be retrieved.</summary>
            public TPartnerInfoScopeEnum PartnerInfoScope
            {
                get
                {
                    return FPartnerInfoScope;
                }
                set
                {
                    FPartnerInfoScope = value;
                }
            }

            /// <summary>Scope of data that is already available client-side.</summary>
            public TPartnerInfoAvailDataEnum Scope
            {
                get
                {
                    return FScope;
                }
                set
                {
                    FScope = value;
                }
            }

            /// <summary>Determines whether the 'rest' of the data needs to be retrieved as well.</summary>
            public bool LoadRestOfData
            {
                get
                {
                    return FLoadRestOfData;
                }
                set
                {
                    FLoadRestOfData = value;
                }
            }

            /// <summary>Holds return value of the server call.</summary>
            public bool ServerCallOK
            {
                get
                {
                    return FServerCallOK;
                }
                set
                {
                    FServerCallOK = value;
                }
            }

            /// <summary>
            /// Construcor.
            /// </summary>
            /// <param name="APartnerKey">PartnerKey of Partner that should be retrieved.</param>
            /// <param name="ALocationKey">Location Primary Key of Partner that should be retrieved.</param>
            /// <param name="AScope">Scope of data that is already available client-side.</param>
            /// <param name="ALoadRestOfData">Determines whether the 'rest' of the data needs to be retrieved as well.</param>
            /// <param name="APartnerInfoScope">Scope of data that should be retrieved.</param>
            public TServerCallParams(Int64 APartnerKey, TLocationPK ALocationKey,
                TPartnerInfoAvailDataEnum AScope,
                bool ALoadRestOfData,
                TPartnerInfoScopeEnum APartnerInfoScope)
            {
                FPartnerKey = APartnerKey;
                FLocationKey = ALocationKey;
                FLoadRestOfData = ALoadRestOfData;
                FScope = AScope;
                FPartnerInfoScope = APartnerInfoScope;

//                TLogging.Log("Created new TServerCallParams. PartnerKey: " + FPartnerKey.ToString() +
//                             "; LocationKey: " + FLocationKey.LocationKey.ToString() +
//                             "; PartnerInfoScope: " + FPartnerInfoScope.ToString("G"));
            }

            /// <summary>
            /// Makes an exact copy of this Class and its values. The resulting Class
            /// and its values have no connection (=reference) to the current instance,
            /// that is, the returned copy is fully independent of the current instance.
            /// </summary>
            /// <returns>Exact copy of the instance of this Class (fully independent of
            /// the current instance.)</returns>
            public TServerCallParams GetDeepCopy()
            {
                lock (FServerCallParamsLock)
                {
                    return new TServerCallParams(FPartnerKey, FLocationKey,
                        FScope, FLoadRestOfData, FPartnerInfoScope);
                }
            }

            /// <summary>
            /// Compares the values of a passed-in instance of this class with the values of the
            /// current instance.
            /// </summary>
            /// <param name="AServerCallParams">Another instance of this class.</param>
            /// <returns>True if the values of the passed-in instance are identical with the
            /// values of this instance, otherwise false.</returns>
            public bool IsIdentical(TServerCallParams AServerCallParams)
            {
                if ((FPartnerKey == AServerCallParams.PartnerKey)
                    && (FScope == AServerCallParams.Scope)
                    && (FLoadRestOfData == AServerCallParams.LoadRestOfData)
                    && (FPartnerInfoScope == AServerCallParams.PartnerInfoScope))
                {
                    if (FLocationKey == null)
                    {
                        if ((AServerCallParams.LocationKey == null))
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else if ((FLocationKey.SiteKey == AServerCallParams.LocationKey.SiteKey)
                             && (FLocationKey.LocationKey == AServerCallParams.LocationKey.LocationKey))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// event triggered when the process of getting the data for the partner has been finished
        /// </summary>
        public event TPartnerKeyDataEventHandler DataLoaded;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public TUC_PartnerInfo()
        {
            //
            // The InitializeComponent() call is required for Windows Forms designer support.
            //
            InitializeComponent();
            #region CATALOGI18N

            // this code has been inserted by GenerateI18N, all changes in this region will be overwritten by GenerateI18N
            this.lblPartnerKey.Text = Catalog.GetString("Partner Key:");
            this.lblAcquisitionCode.Text = Catalog.GetString("Acquired:");
            this.lblStatusCode.Text = Catalog.GetString("Status:");
            this.lblPartnerName.Text = Catalog.GetString("Name:");
            this.tbpAddress.Text = Catalog.GetString("Address");
            this.tbpAddress.ToolTipText = Catalog.GetString("Details of the selected Address");
            this.lblFax.Text = Catalog.GetString("Fax:");
            this.lblTelephone.Text = Catalog.GetString("Telephone:");
            this.lblAlternate.Text = Catalog.GetString("Alternate:");
            this.lblMobile.Text = Catalog.GetString("Mobile:");
            this.lblEmail.Text = Catalog.GetString("Email:");
            this.lblLocationType.Text = Catalog.GetString("Location Type:");
            this.lblMailingAddress.Text = Catalog.GetString("Mailing Addr.:");
            this.lblUrl.Text = Catalog.GetString("URL:");
            this.lblValidFrom.Text = Catalog.GetString("Valid From:");
            this.lblValidTo.Text = Catalog.GetString("Valid To:");
            this.tbpTypesSubscr.Text = Catalog.GetString("Types && Subscriptions");
            this.tbpTypesSubscr.ToolTipText = Catalog.GetString("Subscriptions and Special Types of the Partner");
            this.lblSpecialTypes.Text = Catalog.GetString("Special Types:");
            this.lblSubscriptions.Text = Catalog.GetString("Subscriptions:");
            this.tbpOther.Text = Catalog.GetString("Other");
            this.tbpOther.ToolTipText = Catalog.GetString("Other details of the Partner");
            this.pnlOtherInfo.Text = Catalog.GetString("Other Information:");
            this.lblPreviousName.Text = Catalog.GetString("Previous Name:");
            this.lblParentUnit.Text = Catalog.GetString("Parent Unit:");
            this.lblField.Text = Catalog.GetString("Field:");
            this.lblPartnerUpdated.Text = Catalog.GetString("Partner Updated:");
            this.lblLanguages.Text = Catalog.GetString("guages):");
            this.label4.Text = Catalog.GetString("Speaks (Lan-");
            this.lblLastContact.Text = Catalog.GetString("Last Contact:");
            this.tbpPersonFamily.Text = Catalog.GetString("Person / Family");
            this.lblFamily.Text = Catalog.GetString("Family:");
            this.lblDateOfBirth.Text = Catalog.GetString("Date of Birth:");
            this.lblFamilyMembers.Text = Catalog.GetString("Family Members:");
            this.lblNoPartner.Text = Catalog.GetString("No Partner to display.");
            #endregion

            // TODO: need PetraUtilsObject
            // this.SetStatusBarText(this.tabPartnerDetailInfo, "Select a Tab for more information about this Partner");


            // manual translation is necessary because of line break
            lblLoadingPartnerLocation.Text = Catalog.GetString("Loading...") + Environment.NewLine + Catalog.GetString("Please wait.");
            lblLoadingAddress.Text = Catalog.GetString("Loading...") + Environment.NewLine + Catalog.GetString("Please wait.");
            lblLoadingTypesSubscr.Text = Catalog.GetString("Loading...") + Environment.NewLine + Catalog.GetString("Please wait.");
            lblLoadingOther.Text = Catalog.GetString("Loading...") + Environment.NewLine + Catalog.GetString("Please wait.");
            lblLoadingPersonFamily.Text = Catalog.GetString("Loading...") + Environment.NewLine + Catalog.GetString("Please wait.");

            // Initially we don't show Partner Info data (no Partner loaded yet...).
            ClearControls(false);

            // Set up the Delegate that will be passed to the Timer for the fetching of data
            FDataFetcher = new TDataFetcher(FetchDataFromServer);

            // Set up Timer for the fetching of data
            FTimer = new System.Threading.Timer(new TimerCallback(FDataFetcher),
                null, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);

            // Set up lock object that is used for thread-save access to FServerCallParams.
            FServerCallParamsLock = new object();
        }

        /// <summary>
        /// Stops the Timer for the fetching of data. This is needed to allow a garbage-collection of the UserControl
        /// (a System.Threading.Timer is an unmanaged resource and needs manual Disposal, plus it holds a reference to the Class that instantiated it!)
        /// </summary>
        public void StopTimer()
        {
            FTimer.Dispose();
            FTimer = null;
        }

        /// <summary>
        /// Call this Method to pass in Partner Data that consists of 'Head' and Location data.
        /// The UserControl will make a round-trip to the PetraServer and fetch the rest of the Partner Info data.
        /// </summary>
        /// <param name="APartnerKey">Partner Key of Partner for which Partner Info data should be retrieved.</param>
        /// <param name="APartnerDR">DataRow containing Partner and Location data.</param>
        public void PassPartnerDataPartialWithLocation(Int64 APartnerKey, DataRow APartnerDR)
        {
            UpdateControls(APartnerKey, APartnerDR, null, TPartnerInfoAvailDataEnum.piadHeadAndLocationOnly);
        }

        /// <summary>
        /// Call this Method to pass in Partner Data that consists of 'Head' data.
        /// The UserControl will make a round-trip to the PetraServer and fetch the rest of the Partner Info data.
        /// </summary>
        /// <param name="APartnerKey">Partner Key of Partner for which Partner Info data should be retrieved.</param>
        /// <param name="APartnerDR">DataRow containing Partner data.</param>

        public void PassPartnerDataPartialWithoutLocation(Int64 APartnerKey, DataRow APartnerDR)
        {
            UpdateControls(APartnerKey, APartnerDR, null, TPartnerInfoAvailDataEnum.piadHeadOnly);
        }

        /// <summary>
        /// Call this Method to pass in no Partner Data at all.
        /// The UserControl will make a round-trip to the PetraServer and fetch all of the Partner Info data.
        /// Location and PartnerLocation Data will be fetched for the 'Best Address' of the Partner!
        /// </summary>
        /// <param name="APartnerKey">Partner Key of Partner for which Partner Info data should be retrieved.</param>
        public void PassPartnerDataNone(Int64 APartnerKey)
        {
            UpdateControls(APartnerKey, null, null, TPartnerInfoAvailDataEnum.piadNone);
        }

        /// <summary>
        /// Call this Method to pass in no Partner Data at all.
        /// The UserControl will make a round-trip to the PetraServer and fetch all of the Partner Info data.
        /// Location and PartnerLocation Data will be fetched for the 'Best Address' of the Partner!
        /// </summary>
        /// <param name="APartnerKey">Partner Key of Partner for which Partner Info data should be retrieved.</param>
        /// <param name="ALocationPK">Location PrimaryKey of the Location for which Address data should be retrieved.</param>
        public void PassPartnerDataNone(Int64 APartnerKey, TLocationPK ALocationPK)
        {
            UpdateControls(APartnerKey, null, ALocationPK, TPartnerInfoAvailDataEnum.piadNone);
        }

        /// <summary>
        /// Call this Method to pass in Partner Data that consists of 'Head', Location and 'Rest' data.
        /// The UserControl will make NO round-trip to the PetraServer because Partner Info data is fully passed in!
        /// </summary>
        /// <remarks>This Method isn't functional yet!</remarks>
        /// <param name="APartnerKey">Partner Key of Partner for which Partner Info data should be retrieved.</param>
        /// <param name="APartnerDR">DataRow containing Partner data, Location Data, Partner Location Data and 'Rest' data.</param>
        /// <exception cref="NotImplementedException">Thrown no matter what is passed in because this Method isn't functional yet!</exception>
        public void PassPartnerDataFull(Int64 APartnerKey, DataRow APartnerDR)
        {
            throw new NotImplementedException();

            //            UpdateControls(APartnerKey, APartnerDR, TDataBindingScopeEnum.dbsFull);
        }

        private void OnDataLoaded()
        {
            if (DataLoaded != null)
            {
                DataLoaded(this, new Ict.Petra.Client.App.Gui.Types.TPartnerKeyData(FPartnerKey, FPartnerShortName, FPartnerClass));
            }
        }

        /// <summary>
        /// Fetches Partner Info data from the Petra Server.
        /// </summary>
        /// <param name="ADummy">A dummy object (needed because called from the Timer). Pass in null.</param>
        private void FetchDataFromServer(object ADummy)
        {
            TimeSpan Duration;
            PartnerInfoTDS PartlyPopulatedPartnerInfoDS = null;

            if (FTimer == null)  // prevent execution of this Method if Timer is already disposed!
            {
                return;
            }

            if (FLastDataFeched != DateTime.MinValue)
            {
                Duration = DateTime.Now.Subtract(FLastDataFeched);
            }
            else
            {
                Duration = new TimeSpan(0, 0, 0, 0, MINIMUM_SERVERCALL_GAP);
            }

            // Decide whether to make the call to the PetraServer, or whether to delay it.
            if (Duration.TotalMilliseconds < MINIMUM_SERVERCALL_GAP)
            {
//                TLogging.Log("MINIMUM_SERVERCALL_GAP NOT reached, sleeping " +
//                             (MINIMUM_SERVERCALL_GAP - Duration.TotalMilliseconds).ToString() + "ms ...");

                FTimer.Change(Convert.ToInt32(MINIMUM_SERVERCALL_GAP - Duration.TotalMilliseconds),
                    System.Threading.Timeout.Infinite);
            }
            else
            {
                // we are now in the process of fetching data from the PetraServer!
                FFetchingData = true;
                FLastDataFeched = DateTime.Now;

                // We need to work with an exact copy of FServerCallParams because FServerCallParams
                // can be overwritten anytime!
                FCurrentServerCallParams = FServerCallParams.GetDeepCopy();

//                TLogging.Log("MINIMUM_SERVERCALL_GAP reached, making Server call for Partner " +
//                             FCurrentServerCallParams.PartnerKey.ToString() + ". FCurrentServerCallParams.PartnerInfoScope: " +
//                             FCurrentServerCallParams.PartnerInfoScope.ToString("G") + "...");


                FServerCallError = String.Empty;
                FCurrentServerCallParams.ServerCallOK = false;

                try
                {
                    /*
                     * Actual PetraServer calls!
                     */
                    if ((FCurrentServerCallParams.PartnerInfoScope == TPartnerInfoScopeEnum.pisPartnerLocationAndRestOnly)
                        || (FCurrentServerCallParams.PartnerInfoScope == TPartnerInfoScopeEnum.pisLocationPartnerLocationAndRestOnly))
                    {
                        FCurrentServerCallParams.ServerCallOK = TServerLookup.TMPartner.PartnerInfo(FCurrentServerCallParams.PartnerKey,
                            FCurrentServerCallParams.LocationKey,
                            FCurrentServerCallParams.PartnerInfoScope,
                            out FPartnerInfoDS);
                    }
                    else if (FCurrentServerCallParams.PartnerInfoScope == TPartnerInfoScopeEnum.pisFull)
                    {
                        FCurrentServerCallParams.ServerCallOK = TServerLookup.TMPartner.PartnerInfo(FCurrentServerCallParams.PartnerKey,
                            FCurrentServerCallParams.PartnerInfoScope,
                            out FPartnerInfoDS);

                        FPartnerClass = SharedTypes.PartnerClassStringToEnum(FPartnerInfoDS.PartnerHeadInfo[0].PartnerClass);
                        FPartnerShortName = FPartnerInfoDS.PartnerHeadInfo[0].PartnerShortName;
                    }
                    else
                    {
                        FCurrentServerCallParams.ServerCallOK = TServerLookup.TMPartner.PartnerInfo(FCurrentServerCallParams.PartnerKey,
                            FCurrentServerCallParams.LocationKey,
                            FCurrentServerCallParams.PartnerInfoScope,
                            out PartlyPopulatedPartnerInfoDS);
                    }
                }
                catch (ESecurityPartnerAccessDeniedException Exp)
                {
                    FServerCallError = TMessages.MsgSecurityExceptionString(Exp, this.GetType());
                }
                catch (Exception)
                {
                    FServerCallError = "An error occured while trying to retrieve data for the Partner.";
                }

//MessageBox.Show("FCurrentServerCallParams.ServerCallOK: " + FCurrentServerCallParams.ServerCallOK.ToString());
                if (FServerCallError == String.Empty)
                {
                    /*
                     * Find out if the User requested a different Partner/LocationKey combination
                     * in the meantime. If so, don't update the UI with the currently retrieved data,
                     * but retrive the data for the new Partner/LocationKey combination and update the
                     * UI with that!
                     */
                    if (FCurrentServerCallParams.IsIdentical(FServerCallParams))
                    {
                        if ((FCurrentServerCallParams.PartnerInfoScope == TPartnerInfoScopeEnum.pisPartnerLocationOnly)
                            || (FCurrentServerCallParams.PartnerInfoScope == TPartnerInfoScopeEnum.pisLocationPartnerLocationOnly))
                        {
                            FPartnerInfoDS.PLocation.Rows.Clear();
                            FPartnerInfoDS.PPartnerLocation.Rows.Clear();
                            FPartnerInfoDS.Merge(PartlyPopulatedPartnerInfoDS);
                        }

                        /*
                         * Update the User Interface with the data that was retrieved from the PetraServer!
                         */
                        UpdateUI();

                        // we don't fetch data from the PetraServer anymore.
                        FFetchingData = false;
                    }
                    else
                    {
                        //                    TLogging.Log("Newer FServerCallParams available; making new Server call!");
                        FetchDataFromServer(null);
                    }
                }
                else
                {
                    /*
                     * An Exception occured while making the call to the PetraServer ->
                     * inform user about that!
                     */
                    UpdateUI();

                    // we don't fetch data from the PetraServer anymore.
                    FFetchingData = false;
                }
            }
        }

        /// <summary>
        /// Update the User Interface with the data that was retrieved from the PetraServer.
        /// </summary>
        /// <remarks>This Method determines for itself whether it is running on the UI Tread
        /// and runs itself on the UI Thread if it isn't already doing so!</remarks>
        private void UpdateUI()
        {
            TMyUpdateDelegate MyUpdateDelegate;

            if (txtAcquisitionCode.InvokeRequired)
            {
//                MessageBox.Show("txtAcquisitionCode.InvokeRequired: yes");


                // We are not running on the UI Thread -> need to use Invoke to
                // make us run on the UI Thread!
                MyUpdateDelegate = new TMyUpdateDelegate(UpdateUI);

                try
                {
                    txtAcquisitionCode.Invoke(MyUpdateDelegate, new object[] { });
                }
                catch (System.ComponentModel.InvalidAsynchronousStateException)
                {
                    /*
                     * This Exception occurs when the Partner Find screen
                     * is closed while this UserControl is still fetching data.
                     * --> don't try to do any further action!
                     */
                    MyUpdateDelegate = null;
                }
                catch (Exception)
                {
                    throw;
                }


//                MessageBox.Show("Invoke finished!");
            }
            else
            {
                // We are running on the UI Thread -> go ahead and update the UI!

                try
                {
                    if (FServerCallError == String.Empty)
                    {
                        /*
                         * Update Controls with the Data from the passed in DataRow APartnerDR
                         * and with data from the DB, if necessary.
                         */
                        switch (FCurrentServerCallParams.Scope)
                        {
                            case TPartnerInfoAvailDataEnum.piadHeadOnly:
                            case TPartnerInfoAvailDataEnum.piadNone:

                                if ((FCurrentServerCallParams.ServerCallOK)
                                    && (FPartnerInfoDS.PPartnerLocation.Rows.Count > 0))
                                {
//                                                            MessageBox.Show(FPartnerInfoDS.PLocation.Rows[0][PLocationTable.GetStreetNameDBName()].ToString());

//                                                            if(FPartnerInfoDS.PPartnerType.Rows.Count > 0)
//                                                            {
//                                                                MessageBox.Show(FPartnerInfoDS.PPartnerType[0].TypeCode);
//                                                            }

                                    FLocationDataServerRetrieved = true;
                                    UpdateControlsLocationData();

                                    UpdateControlsRestData();

                                    if (FCurrentServerCallParams.Scope == TPartnerInfoAvailDataEnum.piadNone)
                                    {
                                        FHeadDataServerRetrieved = true;
                                        UpdateControlsHeadData();

                                        ShowHideLoadingInfoFullSize(false);
                                    }
                                }
                                else
                                {
                                    ClearControls(false, FCurrentServerCallParams.PartnerKey);
                                    ShowHideLoadingInfo(false, false, FCurrentServerCallParams.LoadRestOfData);
                                }

                                ShowHideLoadingInfo(false, true, FCurrentServerCallParams.LoadRestOfData);

                                break;

                            case TPartnerInfoAvailDataEnum.piadHeadAndLocationOnly:

                                if ((FCurrentServerCallParams.ServerCallOK)
                                    && (FPartnerInfoDS.PPartnerLocation.Rows.Count > 0))
                                {
                                    //                        MessageBox.Show(FPartnerInfoDS.PPartnerLocation.Rows[0][PPartnerLocationTable.GetTelephoneNumberDBName()].ToString());
                                    //
                                    //                        if(FPartnerInfoDS.PPartnerType.Rows.Count > 0)
                                    //                        {
                                    //                            MessageBox.Show(FPartnerInfoDS.PPartnerType[0].TypeCode);
                                    //                        }

                                    UpdateControlsRestData();
                                }
                                else
                                {
                                    ClearControls(false, FCurrentServerCallParams.PartnerKey);
                                    ShowHideLoadingInfo(false, false, FCurrentServerCallParams.LoadRestOfData);
                                }

                                ShowHideLoadingInfo(false, false, FCurrentServerCallParams.LoadRestOfData);

                                break;
                        }

                        /*
                         * Show 'Person / Family' Tab for PERSONs and FAMILYs
                         */
                        if ((FPartnerClass == TPartnerClass.PERSON)
                            || (FPartnerClass == TPartnerClass.FAMILY))
                        {
                            ShowHidePersonFamilyTab(true);
                        }
                        else
                        {
                            ShowHidePersonFamilyTab(false);
                        }

                        OnDataLoaded();
                    }
                    else
                    {
                        /*
                         * An Exception occured while making the call to the PetraServer ->
                         * inform user about that!
                         */
                        ClearControls(false);
                        ShowHideLoadingInfo(false, false, false);
                    }
                }
                catch (Exception Exp)
                {
                    TLogging.Log("Exception in TUC_PartnerInfo.UpdateUI: " + Exp.ToString());
#if DEBUGMODE
                    MessageBox.Show("Exception in TUC_PartnerInfo.UpdateUI: " + Exp.ToString());
#endif
                }
            }
        }

        /// <summary>
        /// Main Method for updating the data that is displayed in the UserControl.
        /// </summary>
        /// <param name="APartnerKey">Partner Key of Partner for which Partner Info data should be retrieved.</param>
        /// <param name="APartnerDR">DataRow containing Partner data, Location Data, Partner Location Data and 'Rest' data.</param>
        /// <param name="ALocationPK">Location PrimaryKey of the Location for which Address data should be retrieved.
        /// If it is null, it is constructed from data in APartnerDR!</param>
        /// <param name="AScope">Scope of data that is already available client-side.</param>
        private void UpdateControls(Int64 APartnerKey, DataRow APartnerDR, TLocationPK ALocationPK, TPartnerInfoAvailDataEnum AScope)
        {
            TLocationPK LocationPK;
            bool LoadRestOfData;

//            TLogging.Log("Received UpdateControls request for Partner " + APartnerKey.ToString() + ".");

            FPartnerKey = APartnerKey;
            FPartnerDR = APartnerDR;

            if (ALocationPK != null)
            {
                LocationPK = ALocationPK;
            }
            else
            {
                LocationPK = new TLocationPK(-1, -1);
            }

            if (FPartnerDR != null)
            {
                FPartnerClass = SharedTypes.PartnerClassStringToEnum(
                    FPartnerDR[PPartnerTable.GetPartnerClassDBName()].ToString());
                FPartnerShortName = APartnerDR[PPartnerTable.GetPartnerShortNameDBName()].ToString();

                if (LocationPK.LocationKey == -1)
                {
                    LocationPK = new TLocationPK(
                        Convert.ToInt64(APartnerDR[PLocationTable.GetSiteKeyDBName()]),
                        Convert.ToInt32(APartnerDR[PLocationTable.GetLocationKeyDBName()]));
                }
            }

            LoadRestOfData = FPartnerKey != FLastPartnerKey;

            ClearControls(!LoadRestOfData);

            lblNoPartner.Visible = false;
            pnlKeyInfo.Visible = true;

            /*
             * Update Controls with the Data from the passed in DataRow APartnerDR
             * and with data from the DB, if necessary.
             */
            switch (AScope)
            {
                case TPartnerInfoAvailDataEnum.piadHeadOnly:
                {
                    ShowHideLoadingInfo(true, true, LoadRestOfData);

                    FHeadDataServerRetrieved = false;
                    UpdateControlsHeadData();

                    if (LoadRestOfData)
                    {
                        lock (FServerCallParamsLock)
                        {
                            FServerCallParams = new TServerCallParams(APartnerKey,
                                LocationPK,
                                AScope,
                                LoadRestOfData,
                                TPartnerInfoScopeEnum.pisLocationPartnerLocationAndRestOnly);
                        }

                        if (!FFetchingData)
                        {
                            // Request Partner Info data from the PetraServer
                            FTimer.Change(0, System.Threading.Timeout.Infinite);
                        }
                    }
                    else
                    {
                        lock (FServerCallParamsLock)
                        {
                            FServerCallParams = new TServerCallParams(APartnerKey,
                                LocationPK,
                                AScope,
                                LoadRestOfData,
                                TPartnerInfoScopeEnum.pisLocationPartnerLocationOnly);
                        }

                        if (!FFetchingData)
                        {
                            // Request Partner Info data from the PetraServer
                            FTimer.Change(0, System.Threading.Timeout.Infinite);
                        }
                    }

                    break;
                }

                case TPartnerInfoAvailDataEnum.piadHeadAndLocationOnly:
                {
                    ShowHideLoadingInfo(true, false, LoadRestOfData);

                    FHeadDataServerRetrieved = false;
                    UpdateControlsHeadData();

                    FLocationDataServerRetrieved = false;
                    UpdateControlsLocationData();

                    if (LoadRestOfData)
                    {
                        lock (FServerCallParamsLock)
                        {
                            FServerCallParams = new TServerCallParams(APartnerKey,
                                LocationPK,
                                AScope,
                                LoadRestOfData,
                                TPartnerInfoScopeEnum.pisPartnerLocationAndRestOnly);
                        }

                        if (!FFetchingData)
                        {
                            // Request Partner Info data from the PetraServer
                            FTimer.Change(0, System.Threading.Timeout.Infinite);
                        }
                    }
                    else
                    {
                        lock (FServerCallParamsLock)
                        {
                            FServerCallParams = new TServerCallParams(APartnerKey,
                                LocationPK,
                                AScope,
                                LoadRestOfData,
                                TPartnerInfoScopeEnum.pisPartnerLocationOnly);
                        }

                        if (!FFetchingData)
                        {
                            // Request Partner Info data from the PetraServer
                            FTimer.Change(0, System.Threading.Timeout.Infinite);
                        }
                    }

                    break;
                }

                case TPartnerInfoAvailDataEnum.piadNone:
                {
                    ShowHideLoadingInfoFullSize(true);

                    lock (FServerCallParamsLock)
                    {
                        FServerCallParams = new TServerCallParams(APartnerKey,
                            LocationPK,
                            AScope,
                            LoadRestOfData,
                            TPartnerInfoScopeEnum.pisFull);
                    }

                    if (!FFetchingData)
                    {
                        // Request Partner Info data from the PetraServer
                        FTimer.Change(0, System.Threading.Timeout.Infinite);
                    }

                    break;
                }

                case TPartnerInfoAvailDataEnum.piadFull:
                {
                    ShowHideLoadingInfo(true, true, LoadRestOfData);

                    FHeadDataServerRetrieved = false;
                    UpdateControlsHeadData();

                    FLocationDataServerRetrieved = false;
                    UpdateControlsLocationData();

                    UpdateControlsRestData();

                    ShowHideLoadingInfo(false, true, LoadRestOfData);

                    break;
                }
            }

            FLastPartnerKey = FPartnerKey;
        }

        /// <summary>
        /// Clears the display of Partner Info.
        /// </summary>
        public void ClearControls()
        {
            ClearControls(false);
        }

        private void ClearControls(bool AClearOnlyAddressData)
        {
            ClearControls(AClearOnlyAddressData, 0);
        }

        private void ClearControls(bool AClearOnlyAddressData, Int64 APartnerNotFound)
        {
            pnlKeyInfo.Visible = false;
            lblNoPartner.BringToFront();
            lblNoPartner.Visible = true;

            if (FServerCallError == String.Empty)
            {
                if (APartnerNotFound == 0)
                {
                    lblNoPartner.Text = "No Partner to display.";
                }
                else
                {
                    lblNoPartner.Text = String.Format(
                        Resourcestrings.StrPartnerOrLocationNotExistantTitle, APartnerNotFound) +
                                        "." + Environment.NewLine +
                                        Resourcestrings.StrPartnerOrLocationNotExistantText +
                                        Environment.NewLine + Environment.NewLine +
                                        Resourcestrings.StrPartnerOrLocationNotExistantTextReRunSearchText;
                }
            }
            else
            {
                /*
                 * An Exception occured while making the call to the PetraServer ->
                 * inform user about that!
                 */
                lblNoPartner.Text = FServerCallError;
            }

            if (AClearOnlyAddressData)
            {
                txtAddress1.Text = "";

                txtAlternate.Text = "";
                txtLocationType.Text = "";
                txtMailingAddress.Text = "";
                txtMobile.Text = "";
                txtEmail.Text = "";
                txtFax.Text = "";
                txtTelephone.Text = "";
                txtUrl.Text = "";
                txtValidFrom.Text = "";
                txtValidTo.Text = "";
            }
            else
            {
                txtPartnerName.Text = "";
                txtPartnerKey.PartnerKey = 0;
                txtStatusCode.Text = "";
                txtAcquisitionCode.Text = "";

                txtSpecialTypes.Text = "";
                txtSubscriptions.Text = "";

                txtLanguages.Text = "";
                txtLastContact.Text = "";
                txtPartnerUpdated.Text = "";

                txtField.Text = "N/A (not implemented yet...)";
                txtParentUnit.Text = "";
                txtPreviousName.Text = "";
                txtNotes.Text = "";

                txtDateOfBirth.Text = "";
                txtFamilyMembers.Text = "";
            }
        }

        private void ShowHidePersonFamilyTab(bool AShow)
        {
            if (AShow)
            {
                // Show the TabPage only if it isn't already shown
                if (!tabPartnerDetailInfo.Controls.Contains(tbpPersonFamily))
                {
                    tabPartnerDetailInfo.Controls.Add(tbpPersonFamily);
                }
            }
            else
            {
                if (tabPartnerDetailInfo.Controls.Contains(tbpPersonFamily))
                {
                    tabPartnerDetailInfo.Controls.Remove(tbpPersonFamily);
                }
            }
        }

        private void ShowHideLoadingInfo(bool AShow, bool AIncludeLocationLoadingInfo, bool AIncludeRestLoadingInfo)
        {
            lblLoadingPartnerLocation.Visible = AShow;

            if (AIncludeLocationLoadingInfo)
            {
                lblLoadingAddress.Visible = AShow;
            }

            if (AIncludeRestLoadingInfo)
            {
                lblLoadingTypesSubscr.Visible = AShow;
                lblLoadingOther.Visible = AShow;
                lblLoadingPersonFamily.Visible = AShow;
            }

            // Those Labels are at the back of all Controls -
            // bring them to the front so that they are visible.
            if (AShow)
            {
                lblLoadingPartnerLocation.BringToFront();
                lblLoadingAddress.BringToFront();
                lblLoadingTypesSubscr.BringToFront();
                lblLoadingOther.BringToFront();
                lblLoadingPersonFamily.BringToFront();

                this.Cursor = Cursors.AppStarting;
            }
            else
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void ShowHideLoadingInfoFullSize(bool AShow)
        {
            if (AShow)
            {
                pnlKeyInfo.Visible = false;
                lblNoPartner.Visible = true;
                lblNoPartner.Text = "Loading...";
                lblNoPartner.Font = lblLoadingAddress.Font;
                lblNoPartner.BringToFront();

                this.Cursor = Cursors.AppStarting;
            }
            else
            {
                lblNoPartner.SendToBack();
                lblNoPartner.Font = new Font(this.Font, FontStyle.Italic);
                pnlKeyInfo.Visible = true;

                this.Cursor = Cursors.Default;
            }
        }

        /// <summary>
        /// Update 'Head' Controls' Texts
        /// </summary>
        private void UpdateControlsHeadData()
        {
            PartnerInfoTDSPartnerHeadInfoRow PartnerHeadInfoDR;

            if (!FHeadDataServerRetrieved)
            {
                txtPartnerName.Text = FPartnerDR[PPartnerTable.GetPartnerShortNameDBName()].ToString() + "   (" +
                                      FPartnerDR[PPartnerTable.GetPartnerClassDBName()].ToString() + ")";
                txtPartnerKey.PartnerKey = Convert.ToInt64(FPartnerDR[PPartnerTable.GetPartnerKeyDBName()]);
                txtStatusCode.Text = FPartnerDR[PPartnerTable.GetStatusCodeDBName()].ToString();
                txtAcquisitionCode.Text = FPartnerDR[PPartnerTable.GetAcquisitionCodeDBName()].ToString();
            }
            else
            {
                PartnerHeadInfoDR = FPartnerInfoDS.PartnerHeadInfo[0];

                txtPartnerName.Text = PartnerHeadInfoDR.PartnerShortName + "   (" +
                                      PartnerHeadInfoDR.PartnerClass + ")";
                txtPartnerKey.PartnerKey = PartnerHeadInfoDR.PartnerKey;
                txtStatusCode.Text = PartnerHeadInfoDR.StatusCode;
                txtAcquisitionCode.Text = PartnerHeadInfoDR.AcquisitionCode;
            }
        }

        /// <summary>
        /// Update Location Controls' Texts
        /// </summary>
        private void UpdateControlsLocationData()
        {
            DataRow LocationDR;
            string CountryName;

            if (FLocationDataServerRetrieved)
            {
                LocationDR = FPartnerInfoDS.PLocation[0];
            }
            else
            {
                LocationDR = FPartnerDR;
            }

            CountryName = Cache_Lookup.TMCommon.DetermineCountryNameFromCode(LocationDR[PLocationTable.GetCountryCodeDBName()].ToString());

            txtAddress1.Text = Calculations.DetermineLocationString("", "",
                LocationDR[PLocationTable.GetLocalityDBName()].ToString(),
                LocationDR[PLocationTable.GetStreetNameDBName()].ToString(),
                LocationDR[PLocationTable.GetAddress3DBName()].ToString(), "",
                LocationDR[PLocationTable.GetCityDBName()].ToString(),
                LocationDR[PLocationTable.GetCountyDBName()].ToString(),
                LocationDR[PLocationTable.GetPostalCodeDBName()].ToString(),
                CountryName);
        }

        /// <summary>
        /// Update PartnerLocation Controls' Texts
        /// </summary>
        private void UpdateControlsPartnerLocationData()
        {
            string FieldValue = "";
            int ControlsVisible = 0;

            // Telephone
            if (!FPartnerInfoDS.PPartnerLocation[0].IsTelephoneNumberNull())
            {
                FieldValue = FPartnerInfoDS.PPartnerLocation[0].TelephoneNumber;

                if (FieldValue != "")
                {
                    txtTelephone.Text = FPartnerInfoDS.PPartnerLocation[0].TelephoneNumber;

                    if (!FPartnerInfoDS.PPartnerLocation[0].IsExtensionNull())
                    {
                        if (FPartnerInfoDS.PPartnerLocation[0].Extension != 0)
                        {
                            txtTelephone.Text = txtTelephone.Text + " Ext. " + FPartnerInfoDS.PPartnerLocation[0].Extension;
                        }
                    }

                    txtTelephone.Visible = true;
                    lblTelephone.Visible = true;
                    tlpPartnerLocation.RowStyles[0].SizeType = SizeType.Absolute;

                    ControlsVisible++;
                }
                else
                {
                    txtTelephone.Visible = false;
                    lblTelephone.Visible = false;
                    tlpPartnerLocation.RowStyles[0].SizeType = SizeType.AutoSize;
                }
            }
            else
            {
                txtTelephone.Visible = false;
                lblTelephone.Visible = false;
                tlpPartnerLocation.RowStyles[0].SizeType = SizeType.AutoSize;
            }

            // Alternate Telephone
            if (!FPartnerInfoDS.PPartnerLocation[0].IsAlternateTelephoneNull())
            {
                FieldValue = FPartnerInfoDS.PPartnerLocation[0].AlternateTelephone;

                if (FieldValue != "")
                {
                    txtAlternate.Text = FPartnerInfoDS.PPartnerLocation[0].AlternateTelephone;
                    txtAlternate.Visible = true;
                    lblAlternate.Visible = true;
                    tlpPartnerLocation.RowStyles[1].SizeType = SizeType.Absolute;

                    ControlsVisible++;
                }
                else
                {
                    txtAlternate.Visible = false;
                    lblAlternate.Visible = false;
                    tlpPartnerLocation.RowStyles[1].SizeType = SizeType.AutoSize;
                }
            }
            else
            {
                txtAlternate.Visible = false;
                lblAlternate.Visible = false;
                tlpPartnerLocation.RowStyles[1].SizeType = SizeType.AutoSize;
            }

            // Fax Number
            if (!FPartnerInfoDS.PPartnerLocation[0].IsFaxNumberNull())
            {
                FieldValue = FPartnerInfoDS.PPartnerLocation[0].FaxNumber;

                if (FieldValue != "")
                {
                    txtFax.Text = FPartnerInfoDS.PPartnerLocation[0].FaxNumber;

                    if (!FPartnerInfoDS.PPartnerLocation[0].IsFaxExtensionNull())
                    {
                        if (FPartnerInfoDS.PPartnerLocation[0].FaxExtension != 0)
                        {
                            txtFax.Text = txtFax.Text + " Ext. " + FPartnerInfoDS.PPartnerLocation[0].FaxExtension;
                        }
                    }

                    txtFax.Visible = true;
                    lblFax.Visible = true;
                    tlpPartnerLocation.RowStyles[4].SizeType = SizeType.Absolute;

                    ControlsVisible++;
                }
                else
                {
                    txtFax.Visible = false;
                    lblFax.Visible = false;
                    tlpPartnerLocation.RowStyles[4].SizeType = SizeType.AutoSize;
                }
            }
            else
            {
                txtFax.Visible = false;
                lblFax.Visible = false;
                tlpPartnerLocation.RowStyles[4].SizeType = SizeType.AutoSize;
            }

            // Mobile Number
            if (!FPartnerInfoDS.PPartnerLocation[0].IsMobileNumberNull())
            {
                FieldValue = FPartnerInfoDS.PPartnerLocation[0].MobileNumber;

                if (FieldValue != "")
                {
                    txtMobile.Text = FieldValue;
                    txtMobile.Visible = true;
                    lblMobile.Visible = true;
                    tlpPartnerLocation.RowStyles[2].SizeType = SizeType.Absolute;

                    ControlsVisible++;
                }
                else
                {
                    txtMobile.Visible = false;
                    lblMobile.Visible = false;
                    tlpPartnerLocation.RowStyles[2].SizeType = SizeType.AutoSize;
                }
            }
            else
            {
                txtMobile.Visible = false;
                lblMobile.Visible = false;
                tlpPartnerLocation.RowStyles[2].SizeType = SizeType.AutoSize;
            }

            // Email Address
            if (!FPartnerInfoDS.PPartnerLocation[0].IsEmailAddressNull())
            {
                FieldValue = FPartnerInfoDS.PPartnerLocation[0].EmailAddress;

                if (FieldValue != "")
                {
                    txtEmail.Text = FieldValue;
                    txtEmail.Visible = true;
                    lblEmail.Visible = true;
                    tlpPartnerLocation.RowStyles[3].SizeType = SizeType.Absolute;

                    ControlsVisible++;
                }
                else
                {
                    txtEmail.Visible = false;
                    lblEmail.Visible = false;
                    tlpPartnerLocation.RowStyles[3].SizeType = SizeType.AutoSize;
                }
            }
            else
            {
                txtEmail.Visible = false;
                lblEmail.Visible = false;
                tlpPartnerLocation.RowStyles[3].SizeType = SizeType.AutoSize;
            }

            // URL
            if (!FPartnerInfoDS.PPartnerLocation[0].IsUrlNull())
            {
                FieldValue = FPartnerInfoDS.PPartnerLocation[0].Url;

                if (FieldValue != "")
                {
                    txtUrl.Text = FieldValue;
                    txtUrl.Visible = true;
                    lblUrl.Visible = true;
                    tlpPartnerLocation.RowStyles[7].SizeType = SizeType.Absolute;

                    ControlsVisible++;
                }
                else
                {
                    txtUrl.Visible = false;
                    lblUrl.Visible = false;
                    tlpPartnerLocation.RowStyles[7].SizeType = SizeType.AutoSize;
                }
            }
            else
            {
                txtUrl.Visible = false;
                lblUrl.Visible = false;
                tlpPartnerLocation.RowStyles[7].SizeType = SizeType.AutoSize;
            }

            // Location Type
            if (!FPartnerInfoDS.PPartnerLocation[0].IsLocationTypeNull())
            {
                FieldValue = FPartnerInfoDS.PPartnerLocation[0].LocationType;

                if (FieldValue != "")
                {
                    txtLocationType.Text = FieldValue;
                    txtLocationType.Visible = true;
                    lblLocationType.Visible = true;
                    tlpPartnerLocation.RowStyles[5].SizeType = SizeType.Absolute;

                    ControlsVisible++;
                }
                else
                {
                    txtLocationType.Visible = false;
                    lblLocationType.Visible = false;
                    tlpPartnerLocation.RowStyles[5].SizeType = SizeType.AutoSize;
                }
            }
            else
            {
                txtLocationType.Visible = false;
                lblLocationType.Visible = false;
                tlpPartnerLocation.RowStyles[5].SizeType = SizeType.AutoSize;
            }

            // Valid From
            if (!FPartnerInfoDS.PPartnerLocation[0].IsDateEffectiveNull())
            {
                FieldValue = StringHelper.DateToLocalizedString(FPartnerInfoDS.PPartnerLocation[0].DateEffective);

                if (FieldValue != "")
                {
                    txtValidFrom.Text = FieldValue;
                    txtValidFrom.Visible = true;
                    lblValidFrom.Visible = true;
                    tlpPartnerLocation.RowStyles[8].SizeType = SizeType.Absolute;

                    ControlsVisible++;
                }
                else
                {
                    txtValidFrom.Visible = false;
                    lblValidFrom.Visible = false;
                    tlpPartnerLocation.RowStyles[8].SizeType = SizeType.AutoSize;
                }
            }
            else
            {
                txtValidFrom.Visible = false;
                lblValidFrom.Visible = false;
                tlpPartnerLocation.RowStyles[8].SizeType = SizeType.AutoSize;
            }

            // Valid To
            if (!FPartnerInfoDS.PPartnerLocation[0].IsDateGoodUntilNull())
            {
                FieldValue = FPartnerInfoDS.PPartnerLocation[0].DateGoodUntil.ToString();

                if (FieldValue != "")
                {
                    txtValidTo.Text = StringHelper.DateToLocalizedString(FPartnerInfoDS.PPartnerLocation[0].DateGoodUntil);
                    txtValidTo.Visible = true;
                    lblValidTo.Visible = true;
                    tlpPartnerLocation.RowStyles[9].SizeType = SizeType.Absolute;

                    ControlsVisible++;
                }
                else
                {
                    txtValidTo.Visible = false;
                    lblValidTo.Visible = false;
                    tlpPartnerLocation.RowStyles[9].SizeType = SizeType.AutoSize;
                }
            }
            else
            {
                txtValidTo.Visible = false;
                lblValidTo.Visible = false;
                tlpPartnerLocation.RowStyles[9].SizeType = SizeType.AutoSize;
            }

            // Mailing Address
            if (!FPartnerInfoDS.PPartnerLocation[0].IsSendMailNull())
            {
                if (FPartnerInfoDS.PPartnerLocation[0].SendMail)
                {
                    txtMailingAddress.Text = "YES";
                }
                else
                {
                    txtMailingAddress.Text = "No";
                }

                ControlsVisible++;
            }
            else
            {
                txtMailingAddress.Text = "No";
            }

            if (ControlsVisible <= 4)
            {
                tlpPartnerLocation.AutoScrollPosition = new Point(0, 0);
            }
        }

        /// <summary>
        /// Update Special Types and Subscriptions Controls' Texts
        /// </summary>
        private void UpdateControlsSpecialTypesSubscriptionsData()
        {
            const string VALUE_SEPARATOR = ", ";
            DataView SpecialTypesDV;
            DataView SubscriptionsDV;
            PSubscriptionRow SubscriptionDR;
            string SpecialTypes = "";
            string Subscriptions = "";

            /*
             * Special Types: Build a String out of the indivual Partner Type DataRows
             */

            // We want the Special Types sorted alphabetically
            SpecialTypesDV = new DataView(FPartnerInfoDS.PPartnerType, "",
                PPartnerTypeTable.GetTypeCodeDBName() + " ASC",
                DataViewRowState.CurrentRows);

            for (int Counter1 = 0; Counter1 < SpecialTypesDV.Count; Counter1++)
            {
                SpecialTypes = SpecialTypes + ((PPartnerTypeRow)(SpecialTypesDV[Counter1].Row)).TypeCode.ToUpper() +
                               VALUE_SEPARATOR;
            }

            // Remove last Separator
            if (SpecialTypes.Length > VALUE_SEPARATOR.Length)
            {
                SpecialTypes = SpecialTypes.Substring(0,
                    SpecialTypes.Length - VALUE_SEPARATOR.Length);
            }

            txtSpecialTypes.Text = SpecialTypes;


            /*
             * Subscriptions: Build a String out of the indivual Subscription DataRows
             */

            // We want the Subscriptions sorted alphabetically
            SubscriptionsDV = new DataView(FPartnerInfoDS.PSubscription, "",
                PSubscriptionTable.GetPublicationCodeDBName() + " ASC",
                DataViewRowState.CurrentRows);

            for (int Counter2 = 0; Counter2 < SubscriptionsDV.Count; Counter2++)
            {
                SubscriptionDR = ((PSubscriptionRow)(SubscriptionsDV[Counter2].Row));

                if ((SubscriptionDR.SubscriptionStatus != MPartnerConstants.SUBSCRIPTIONS_STATUS_CANCELLED)
                    && (SubscriptionDR.SubscriptionStatus != MPartnerConstants.SUBSCRIPTIONS_STATUS_EXPIRED)
                    && (SubscriptionDR.StartDate <= DateTime.Today))
                {
                    Subscriptions = Subscriptions + SubscriptionDR.PublicationCode.ToUpper() +
                                    VALUE_SEPARATOR;
                }
            }

            // Remove last Separator
            if (Subscriptions.Length > VALUE_SEPARATOR.Length)
            {
                Subscriptions = Subscriptions.Substring(0,
                    Subscriptions.Length - VALUE_SEPARATOR.Length);
            }

            txtSubscriptions.Text = Subscriptions;
        }

        /// <summary>
        /// Update 'Other' Controls' Texts. Also updates 'Person/Family' Tab Texts if needed.
        /// </summary>
        private void UpdateControlsOtherData()
        {
            PartnerInfoTDSPartnerAdditionalInfoRow OtherDataDR;

            OtherDataDR = FPartnerInfoDS.PartnerAdditionalInfo[0];

            txtLanguages.Text = Cache_Lookup.TMCommon.DetermineLanguageNameFromCode(OtherDataDR.MainLanguages);
            txtNotes.Text = OtherDataDR.Notes;

            if (!OtherDataDR.IsDateModifiedNull())
            {
                txtPartnerUpdated.Text = StringHelper.DateToLocalizedString(OtherDataDR.DateModified);
            }
            else
            {
                if (!OtherDataDR.IsDateCreatedNull())
                {
                    txtPartnerUpdated.Text = StringHelper.DateToLocalizedString(OtherDataDR.DateCreated);
                }
            }

            if (OtherDataDR.LastContact != DateTime.MinValue)
            {
                txtLastContact.Text = StringHelper.DateToLocalizedString(OtherDataDR.LastContact);
            }

            // TODO: rest of the fields!
            // Field, Parent Unit (Name, PartnerKey)

            if (!OtherDataDR.IsPreviousNameNull())
            {
                txtPreviousName.Text = OtherDataDR.PreviousName;
            }

            switch (FPartnerClass)
            {
                case TPartnerClass.PERSON:

                    if (!OtherDataDR.IsFieldNull())
                    {
                        txtField.Text = OtherDataDR.Field;
                    }

                    UpdateControlsPersonFamilyData(OtherDataDR);
                    UpdateAdditionalLanguages(OtherDataDR);

                    tbpPersonFamily.Text = "Person";
                    pnlOtherRightField.Visible = true;
                    pnlOtherRightParentUnit.Visible = false;

                    break;

                case TPartnerClass.FAMILY:

                    if (!OtherDataDR.IsFieldNull())
                    {
                        txtField.Text = OtherDataDR.Field;
                    }

                    UpdateControlsPersonFamilyData(OtherDataDR);

                    tbpPersonFamily.Text = "Family";
                    pnlOtherRightField.Visible = true;
                    pnlOtherRightParentUnit.Visible = false;

                    break;

                case TPartnerClass.UNIT:
                    UpdateUnitParent();
                    pnlOtherRightField.Visible = false;
                    pnlOtherRightParentUnit.Visible = true;

                    break;

                default:
                    pnlOtherRightField.Visible = false;
                    pnlOtherRightParentUnit.Visible = false;

                    break;
            }
        }

        /// <summary>
        /// Update 'Person/Family' Tab Texts if needed.
        /// </summary>
        private void UpdateControlsPersonFamilyData(PartnerInfoTDSPartnerAdditionalInfoRow AOtherDataDR)
        {
            if (FPartnerClass == TPartnerClass.PERSON)
            {
                pnlDateOfBirth.Visible = true;
                pnlFamily.Visible = true;

                if (!AOtherDataDR.IsDateOfBirthNull())
                {
                    txtDateOfBirth.Text = StringHelper.DateToLocalizedString(AOtherDataDR.DateOfBirth);
                }

                if (!AOtherDataDR.IsFamilyNull())
                {
                    txtFamily.Text = AOtherDataDR.Family + "   [" + AOtherDataDR.FamilyKey + "]";
                }
            }
            else
            {
                pnlDateOfBirth.Visible = false;
                pnlFamily.Visible = false;
            }

            UpdateFamilyMembers();
        }

        private void UpdateAdditionalLanguages(PartnerInfoTDSPartnerAdditionalInfoRow AOtherDataDR)
        {
            const char LANG_SEPARATOR = '|';
            const string LANG_LIST_SEPARATOR = "; ";

            string[] AdditionalLangs;

            if (!AOtherDataDR.IsAdditionalLanguagesNull())
            {
                if (AOtherDataDR.AdditionalLanguages != "")
                {
                    txtLanguages.Text = txtLanguages.Text + " (Main Lang.); Also: ";

                    AdditionalLangs = AOtherDataDR.AdditionalLanguages.Split(new char[] { LANG_SEPARATOR });

                    for (int Counter = 0; Counter < AdditionalLangs.Length; Counter++)
                    {
                        txtLanguages.Text = txtLanguages.Text +
                                            Cache_Lookup.TMCommon.DetermineLanguageNameFromCode(AdditionalLangs[Counter]) +
                                            LANG_LIST_SEPARATOR;
                    }

                    txtLanguages.Text = txtLanguages.Text.Substring(0,
                        txtLanguages.Text.Length - LANG_LIST_SEPARATOR.Length) + ".";
                }
            }
        }

        private void UpdateUnitParent()
        {
            PartnerInfoTDSUnitInfoRow UnitInfoDR;

            if (FPartnerInfoDS.UnitInfo.Rows.Count > 0)
            {
                UnitInfoDR = FPartnerInfoDS.UnitInfo[0];
                txtParentUnit.Text = UnitInfoDR.ParentUnitName + "   [" + UnitInfoDR.ParentUnitKey.ToString() + "]";
            }
        }

        private void UpdateFamilyMembers()
        {
            PartnerInfoTDSFamilyMembersRow FamilyMembersDR;
            string FamilyMembers = "";

            for (int Counter = 0; Counter < FPartnerInfoDS.FamilyMembers.Rows.Count; Counter++)
            {
                FamilyMembersDR = FPartnerInfoDS.FamilyMembers[Counter];
                FamilyMembers = FamilyMembers + FamilyMembersDR.PartnerShortName + "   [" + FamilyMembersDR.PartnerKey + "]" +
                                Environment.NewLine;
            }

            if (FamilyMembers.Length != 0)
            {
                txtFamilyMembers.Text = FamilyMembers.Substring(0, FamilyMembers.Length - Environment.NewLine.Length);
            }
        }

        /// <summary>
        /// Update 'Rest' Controls' Texts.
        /// </summary>
        private void UpdateControlsRestData()
        {
            UpdateControlsPartnerLocationData();

            UpdateControlsSpecialTypesSubscriptionsData();

            UpdateControlsOtherData();
        }
    }

    /// <summary>
    /// event triggered when the process of getting the data for the partner has been finished
    /// </summary>
    public delegate void TPartnerKeyDataEventHandler(System.Object Sender, Ict.Petra.Client.App.Gui.Types.TPartnerKeyData e);
}