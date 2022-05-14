using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;

using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.ADF;
using ESRI.ArcGIS.SystemUI;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using System.Collections.Generic;
using ESRI.ArcGIS.NetworkAnalyst;
using ESRI.ArcGIS.DataSourcesGDB;

namespace MapApp
{
    public sealed partial class MainForm : Form
    {
        string strPath = System.Environment.CurrentDirectory + @"\map\map.mxd"; //��ͼ·��
        #region class private members
        private IMapControl3 m_mapControl = null;
        private string m_mapDocumentName = string.Empty;
        #endregion

        #region class constructor
        public MainForm()
        {
            InitializeComponent();
            m_ipActiveView = axMapControl1.ActiveView;
        }
        #endregion

        private void MainForm_Load(object sender, EventArgs e)
        {
            //get the MapControl
            m_mapControl = (IMapControl3)axMapControl1.Object;

            //���ص�ͼ
            //string sPath = System.IO.Path.Combine(Application.StartupPath, @"test\map.mxd");
            axMapControl1.LoadMxFile(strPath);
            axMapControl1.Refresh();
        }

        #region Main Menu event handlers
        private void menuNewDoc_Click(object sender, EventArgs e)
        {
            //execute New Document command
            ICommand command = new CreateNewDocument();
            command.OnCreate(m_mapControl.Object);
            command.OnClick();
        }

        private void menuOpenDoc_Click(object sender, EventArgs e)
        {
            //execute Open Document command
            ICommand command = new ControlsOpenDocCommandClass();
            command.OnCreate(m_mapControl.Object);
            command.OnClick();
        }

        private void menuSaveDoc_Click(object sender, EventArgs e)
        {
            //execute Save Document command
            if (m_mapControl.CheckMxFile(m_mapDocumentName))
            {
                //create a new instance of a MapDocument
                IMapDocument mapDoc = new MapDocumentClass();
                mapDoc.Open(m_mapDocumentName, string.Empty);

                //Make sure that the MapDocument is not readonly
                if (mapDoc.get_IsReadOnly(m_mapDocumentName))
                {
                    MessageBox.Show("Map document is read only!");
                    mapDoc.Close();
                    return;
                }

                //Replace its contents with the current map
                mapDoc.ReplaceContents((IMxdContents)m_mapControl.Map);

                //save the MapDocument in order to persist it
                mapDoc.Save(mapDoc.UsesRelativePaths, false);

                //close the MapDocument
                mapDoc.Close();
            }
        }

        private void menuSaveAs_Click(object sender, EventArgs e)
        {
            //execute SaveAs Document command
            ICommand command = new ControlsSaveAsDocCommandClass();
            command.OnCreate(m_mapControl.Object);
            command.OnClick();
        }

        private void menuExitApp_Click(object sender, EventArgs e)
        {
            //exit the application
            Application.Exit();
        }
        #endregion

        //listen to MapReplaced evant in order to update the statusbar and the Save menu
        private void axMapControl1_OnMapReplaced(object sender, IMapControlEvents2_OnMapReplacedEvent e)
        {
            //get the current document name from the MapControl
            m_mapDocumentName = m_mapControl.DocumentFilename;

            //if there is no MapDocument, diable the Save menu and clear the statusbar
            if (m_mapDocumentName == string.Empty)
            {
                statusBarXY.Text = string.Empty;
            }
            else
            {
                //enable the Save manu and write the doc name to the statusbar
                statusBarXY.Text = System.IO.Path.GetFileName(m_mapDocumentName);
            }
        }

        private void axMapControl1_OnMouseMove(object sender, IMapControlEvents2_OnMouseMoveEvent e)
        {
            statusBarXY.Text = string.Format("{0}, {1}  {2}", e.mapX.ToString("#######.##"), e.mapY.ToString("#######.##"), axMapControl1.MapUnits.ToString().Substring(4));
        }


        #region ���·��

        IActiveView m_ipActiveView;
        const string MEMORY_WORKSPACE = "WorkSpace_Data";
        const string LAYER_NAME = "Deports Layer";
        public static INALayer CreateRouteLayer(string mxdFileName, List<IPoint> stops)
        {

            INetworkDataset networkDataset = GetNetworkDataSet(mxdFileName);
            IFieldsEdit fieldEditor = new FieldsClass();
            //Create Layer in memory
            IFeatureLayer featureLayer = CreateLayerInMomeoy(MEMORY_WORKSPACE, LAYER_NAME, esriGeometryType.esriGeometryPoint, fieldEditor);
            IFeatureClass featureClass = featureLayer.FeatureClass;

            //insert deport points into memory layer
            IWorkspaceEdit workspaceEdit = ((IDataset)featureClass).Workspace as IWorkspaceEdit;
            workspaceEdit.StartEditing(false);
            workspaceEdit.StartEditOperation();
            IFeatureBuffer featureBuffer = featureClass.CreateFeatureBuffer();
            IFeatureCursor featureCursor = featureClass.Insert(true);

           // featureCursor = pInputFC.Search(null, true);
          //  featureClass = pInputFC;

            //List<IFeature> pFeatures = new List<IFeature>();
            //for(int i=0;i<3;i++)
            //{
            //    IFeature pFeature = pInputFC.GetFeature(i);
            //    IPoint p= pFeature.Shape as IPoint;
            //}
           
            

            //for (int i = 0; i < pFeatures.Count; i++)
            //{
            //    MessageBox.Show(((pFeatures.Shape as IPolygon) as IArea).Area.ToString());
            //}

            foreach (ESRI.ArcGIS.Geometry.Point stop in stops)
            {
                IPoint point = new PointClass();
                point.X = stop.X;
                point.Y = stop.Y;
                featureBuffer.Shape = point as IGeometry;
                featureCursor.InsertFeature(featureBuffer);
                System.Diagnostics.Debug.WriteLine(stop.X.ToString() + "," + stop.Y.ToString());
            }
            featureCursor.Flush();
            workspaceEdit.StopEditOperation();
            workspaceEdit.StopEditing(true);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(featureCursor);

            //Solve
            INAContext naContext = null;
            IGPMessages gpMessage = null;

            naContext = CreateSolverContext(networkDataset);
            LoadNANetworkLocations(ref naContext, "Stops", featureClass, 5000);//Stops
            SetSolverSettings(ref naContext, "����", false); //Length

            gpMessage = (IGPMessages)(new GPMessagesClass());

            if (naContext.Solver.Solve(naContext, gpMessage, null))
            {

            }
            int a = gpMessage.Count;
            INALayer naLayer = naContext.Solver.CreateLayer(naContext);
            
            return naLayer;
        }
        static IFeatureClass pInputFC, pVertexFC;
        private static  INetworkDataset GetNetworkDataSet(string mxdFileName)
        {
            //Open the mxd file and get the network dataset
            IMapDocument pMapDocument = new MapDocumentClass();
            pMapDocument.Open(mxdFileName, "");
            INetworkDataset networkDataset = null;

            //iterate all the maps
            for (int cMap = 0; cMap < pMapDocument.MapCount; cMap++)
            {
                for (int cLayer = 0; cLayer < pMapDocument.get_Map(cMap).LayerCount; cLayer++)
                {
                    ILayer pLayer = pMapDocument.get_Map(cMap).get_Layer(cLayer);
                    //NA Layer
                    if (pLayer is INALayer)
                    {
                        INALayer pNALayer = pLayer as INALayer;
                        networkDataset = pNALayer.Context.NetworkDataset;
                        //break;
                    }
                    #region liyafei
                    //if (pLayer.Name == "ͣ��վ")
                    //{
                    //    if (pLayer is IFeatureLayer)
                    //    {
                    //        pInputFC = (pLayer as IFeatureLayer).FeatureClass;
                    //        // string aa = "";
                    //    }
                    //    //pInputFC = pLayer as IFeatureClass;
                    //}
                    #endregion
                    //Composite Layer
                    if (pLayer is IGroupLayer)
                    {
                        ICompositeLayer pCompositeLayer = pLayer as ICompositeLayer;
                        for (int cContainLayer = 0; cContainLayer < pCompositeLayer.Count; cContainLayer++)
                        {
                            if (pCompositeLayer.get_Layer(cContainLayer) is INALayer)
                            {
                                INALayer pNALayer = pCompositeLayer.get_Layer(cContainLayer) as INALayer;
                                networkDataset = pNALayer.Context.NetworkDataset;
                                break;
                            }
                        }
                    }
                }
            }

            //Close the MXD Document
            pMapDocument.Close();
            ////judge get the net work data set or not
            if (networkDataset == null)
            {
                Exception ex = new Exception();
                ex.Data.Add(0, "�޷����������ݼ�!");
                throw ex;
            }
            return networkDataset;
        }

        public static IFeatureLayer CreateLayerInMomeoy(string wsName, string layerName, esriGeometryType shapeType, IFieldsEdit allFields)
        {
            IWorkspaceName workspaceName = null;
            IWorkspaceFactory workspaceFactory = null;
            IFeatureWorkspace workspace = null;
            IFeatureClass featureClass = null;
            IFeatureLayer featureLayer = null;
            IFieldEdit fieldEdit = null;
            IGeometryDefEdit geometryDefEdit = null;
            ISpatialReference spatialRef = null;

            //Add Shapes Fields
            fieldEdit = new FieldClass();
            fieldEdit.Name_2 = "Shape";
            fieldEdit.Type_2 = esriFieldType.esriFieldTypeGeometry;
            fieldEdit.IsNullable_2 = true;
            fieldEdit.Required_2 = true;
            geometryDefEdit = new GeometryDefClass();
            geometryDefEdit.GeometryType_2 = shapeType;
            spatialRef = new UnknownCoordinateSystemClass();
            spatialRef.SetDomain(-18000, 18000, -18000, 18000);
            geometryDefEdit.SpatialReference_2 = spatialRef;
            fieldEdit.GeometryDef_2 = geometryDefEdit;
            allFields.AddField(fieldEdit);

            //Create memory layer
            workspaceFactory = new InMemoryWorkspaceFactoryClass();
            workspaceName = workspaceFactory.Create("", wsName, null, 0);
            workspace = ((IName)workspaceName).Open() as IFeatureWorkspace;
            featureClass = workspace.CreateFeatureClass(layerName, allFields, null, null, esriFeatureType.esriFTSimple, "Shape", "");
            featureLayer = new FeatureLayerClass();
            featureLayer.Name = layerName;
            featureLayer.FeatureClass = featureClass;

            return featureLayer;
        }
        public static void SetSolverSettings(ref INAContext naContext, string metersName, bool oneWay)
        {
            INASolver naSolver = naContext.Solver;
            INARouteSolver naRouteSolver = (INARouteSolver)naSolver;
            INASolverSettings solverSettings = null;
            IStringArray restrictions = null;
            INAAgent naAgent = null;

            naRouteSolver.OutputLines = esriNAOutputLineType.esriNAOutputLineTrueShapeWithMeasure;
            naRouteSolver.CreateTraversalResult = true;
            naRouteSolver.UseTimeWindows = false;
            naRouteSolver.FindBestSequence = false;
            naRouteSolver.PreserveFirstStop = true;
            naRouteSolver.PreserveLastStop = true;

            solverSettings = (INASolverSettings)naSolver;
            solverSettings.ImpedanceAttributeName = metersName;
            IStringArray accumulateAttributeNames = solverSettings.AccumulateAttributeNames;
            accumulateAttributeNames.Add(metersName);
            solverSettings.AccumulateAttributeNames = accumulateAttributeNames;
            solverSettings.IgnoreInvalidLocations = true;

            restrictions = solverSettings.RestrictionAttributeNames;
            restrictions.RemoveAll();

            if (oneWay)
                restrictions.Add("OneWay");

            solverSettings.RestrictionAttributeNames = restrictions;

            solverSettings.RestrictUTurns = esriNetworkForwardStarBacktrack.esriNFSBNoBacktrack;

            solverSettings.UseHierarchy = false;

            naSolver.UpdateContext(naContext, GetDENetworkDataset(naContext.NetworkDataset), null);

            naAgent = (INAAgent)naContext.Agents.get_ItemByName("StreetDirectionsAgent");
            naAgent.OnContextUpdated();
        }
        public static INAContext CreateSolverContext(INetworkDataset networkDataset)
        {
            IDENetworkDataset deNetworkDataset = null;
            INASolver naSolver = null;
            INAContextEdit naContextEdit = null;
            IGPMessages gpMsg = null;

            deNetworkDataset = GetDENetworkDataset(networkDataset);
            naSolver = new NARouteSolver();
            naContextEdit = (INAContextEdit)(naSolver.CreateContext(deNetworkDataset, "Route"));
            gpMsg = new GPMessagesClass();
            naContextEdit.Bind(networkDataset, gpMsg);

            return (INAContext)naContextEdit;
        }

        public static IDENetworkDataset GetDENetworkDataset(INetworkDataset networkDataset)
        {
            IDatasetComponent datasetComponent = null;
            datasetComponent = (IDatasetComponent)networkDataset;

            ///Get the Data Element
            IDENetworkDataset dataset = null;
            dataset = (IDENetworkDataset)datasetComponent.DataElement;
            return dataset;
        }
        public static void LoadNANetworkLocations(ref INAContext naContext, string naClassName, IFeatureClass inputFeatureClass, double snapTolerance)
        {
            INamedSet naClasses = null;
            INAClass naClass = null;
            INAClassLoader naLoader = null;
            INAClassFieldMap fieldMap = null;
            int rowsIn = 0;
            int rowsLocated = 0;

            naClasses = naContext.NAClasses;
            naClass = (INAClass)(naClasses.get_ItemByName(naClassName));

            naClass.DeleteAllRows();

            naLoader = new NAClassLoaderClass();
            naLoader.Locator = naContext.Locator;
            if (snapTolerance > 0)
            {
                naLoader.Locator.SnapTolerance = snapTolerance;
            }
            naLoader.NAClass = naClass;
            fieldMap = new NAClassFieldMapClass();
            fieldMap.CreateMapping(naClass.ClassDefinition, inputFeatureClass.Fields);
            naLoader.FieldMap = fieldMap;
            naLoader.Load((ICursor)(inputFeatureClass.Search(null, true)), null, ref rowsIn, ref rowsLocated);
            System.Diagnostics.Debug.WriteLine(rowsIn.ToString() + "," + rowsLocated.ToString());
        }
         List<IPoint> ptList=new List<IPoint>();
         
         //string strPath = System.Environment.CurrentDirectory + @"\ex16\ex16.mxd";
        //private void button1_Click(object sender, EventArgs e)
        //{
        //    if (ptList.Count <= 0)
        //    {
        //        MessageBox.Show("������ͼѡ��㣡");
        //    }
        //    else
        //    {
        //        this.axMapControl1.AddLayer(CreateRouteLayer(strPath, ptList) as ILayer);
        //        ptList.Clear();
        //    } 
        //}

        private void axMapControl1_OnMouseDown(object sender, IMapControlEvents2_OnMouseDownEvent e)
        {
            IPoint ipNew;
            ipNew = m_ipActiveView.ScreenDisplay.DisplayTransformation.ToMapPoint(e.x, e.y);
            object o = Type.Missing;
            ptList.Add(ipNew);
        }
        #endregion

        private void menuShortWay_Click(object sender, EventArgs e)
        {
            if (ptList.Count <= 0)
            {
                MessageBox.Show("������ͼѡ��㣡");
            }
            else
            {
                try
                {
                    this.axMapControl1.AddLayer(CreateRouteLayer(strPath, ptList) as ILayer);
                }
                catch(Exception ex)
                {
                    MessageBox.Show("ʧ��:" + ex.Message);
                }
                ptList.Clear();
            } 
        }

        private void ���Ӳ�����Ӧ��ѧУToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectHouse shForm = new SelectHouse();
            shForm.listItem = new List<string>();
            ILayer layer = GetLayer("house");

            IFeatureLayer featureLayer = layer as IFeatureLayer;
            IFeatureClass featureClass = featureLayer.FeatureClass;
            IQueryFilter queryFilter = new QueryFilterClass();
            IFeatureCursor featureCusor;
            queryFilter.WhereClause = "1=1";
            featureCusor = featureClass.Search(queryFilter, true);
            IFeature feature = null;
            int findex = -1;
            //���ҳ����е㲢�������
            while ((feature = featureCusor.NextFeature()) != null)
            {
                if (findex == -1)
                {
                    findex = feature.Fields.FindField("Name");
                }
                shForm.listItem.Add(feature.get_Value(findex).ToString());
            }

            if (shForm.ShowDialog() == DialogResult.OK)
            {
                //MessageBox.Show(shForm.SelectItem);
                //�����߼�

                //���ҳ�����ѧУͼԪ
                queryFilter.WhereClause = String.Format("Name='{0}'", shForm.SelectItem);
                featureCusor = featureClass.Search(queryFilter, true);

                while ((feature = featureCusor.NextFeature()) != null)
                {
                    //feature ���ǲ��ҳ�����ͼԪ �㻺������ѯ

                    //IArea a
                    //IRelationalOperator r;

                    //���ҳ�����ѧ�� ���жϵ�ǰѧУ�����Ƿ������ѧ��
                    ILayer layerxuequ = GetLayer("xuequ");
                    IFeatureLayer featureLayerlayerxuequ = layerxuequ as IFeatureLayer;
                    IFeatureClass featureClasslayerxuequ = featureLayerlayerxuequ.FeatureClass;
                    IQueryFilter queryFilterlayerxuequ = new QueryFilterClass();
                    IFeatureCursor featureCusorlayerxuequ;
                    queryFilterlayerxuequ.WhereClause = "1=1";
                    featureCusorlayerxuequ = featureClasslayerxuequ.Search(queryFilterlayerxuequ, true);
                    IFeature featurelayerxuequ = null;
                    //���ҳ�����ѧ�� ���жϵ�ǰѧУ�����Ƿ������ѧ��
                    while ((featurelayerxuequ = featureCusorlayerxuequ.NextFeature()) != null)
                    {
                        IRelationalOperator relation = featurelayerxuequ.Shape as IRelationalOperator;
                        if(relation.Contains(feature.Shape))
                        {
                            //�Ǹ�ѧ��
                            //axMapControl1.FlashShape(featurelayerxuequ.Shape);
                            //axMapControl1.ActiveView.Extent = featurelayerxuequ.Shape.Envelope;
                            //axMapControl1.Refresh();

                            //int ffindex = featurelayerxuequ.Fields.FindField("Name");
                            //MessageBox.Show(featurelayerxuequ.get_Value(ffindex).ToString());

                            //����ѧУ
                            ILayer schoolLayer = GetLayer("school");

                            IFeatureLayer featureschoolLayer = schoolLayer as IFeatureLayer;
                            IFeatureClass featureschoolClass = featureschoolLayer.FeatureClass;
                            IQueryFilter queryschoolFilter = new QueryFilterClass();
                            IFeatureCursor featureschoolCusor;
                            queryschoolFilter.WhereClause = "1=1";
                            featureschoolCusor = featureschoolClass.Search(queryschoolFilter, true);
                            List<IFeature> listResult = new List<IFeature>();
                            IFeature schoolfeature;

                            DataTable dtTemp = new DataTable();
                            dtTemp.Columns.Add("ѧУ����");
                            dtTemp.Columns.Add("ѧУ����");
                            while ((schoolfeature = featureschoolCusor.NextFeature()) != null)
                            {
                                if (relation.Contains(schoolfeature.Shape))
                                {
                                    //listResult.Add(schoolfeature);
                                    DataRow dr = dtTemp.NewRow();
                                    dr["ѧУ����"] = GetFeatureValue(schoolfeature, "SchoolName");
                                    dr["ѧУ����"] = GetFeatureValue(schoolfeature, "xuexiaopaiming");
                                    dtTemp.Rows.Add(dr);
                                }
                                //if (findex == -1)
                                //{
                                //    findex = feature.Fields.FindField("Name");
                                //}
                                //shForm.listItem.Add(feature.get_Value(findex).ToString());
                            }

                            //Dictionary<string, string> dicFields = new Dictionary<string, string>();
                            //dicFields.Add("SchoolName", "ѧУ����");
                            //dicFields.Add("xuexiaopaiming", "ѧУ����");
                            //DataTable dt = GetDataTable(listResult, dicFields);
                            ShowDatatable frmShow = new ShowDatatable(dtTemp);
                            frmShow.Text = "ѧ���ڵ�Сѧ";
                            frmShow.ShowDialog();
                            return;
                        }
                    }


                }
            }
        }

        private ILayer GetLayer(string LayerName)
        {
            for (int i = 0; i < axMapControl1.Map.LayerCount; i++)
            {
                ILayer pLayer = axMapControl1.Map.get_Layer(i);
                if (pLayer.Name.Equals(LayerName))
                {
                    return pLayer;
                }
            }
            return null;

        }

        private void ѧ������ѯToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectSchoolZone ssForm = new SelectSchoolZone();
            ssForm.listItem = new List<string>();
            ILayer layer = GetLayer("xuequ");

            IFeatureLayer featureLayer = layer as IFeatureLayer;
            IFeatureClass featureClass = featureLayer.FeatureClass;
            IQueryFilter queryFilter = new QueryFilterClass();
            IFeatureCursor featureCusor;
            queryFilter.WhereClause = "1=1";
            featureCusor = featureClass.Search(queryFilter, true);
            IFeature feature = null;
            int findex = -1;
            //���ҳ����е㲢�������
            while ((feature = featureCusor.NextFeature()) != null)
            {
                if (findex == -1)
                {
                    findex = feature.Fields.FindField("Name");
                }
                ssForm.listItem.Add(feature.get_Value(findex).ToString());
            }

            if (ssForm.ShowDialog() == DialogResult.OK)
            {
                //���ҳ�����ѧУͼԪ
                queryFilter.WhereClause = String.Format("Name='{0}'",ssForm.SelectItem);
                featureCusor = featureClass.Search(queryFilter, true);
        
                while ((feature = featureCusor.NextFeature()) != null)
                {
                    //�������з��� �ж��Ƿ������ѧ��
                    IRelationalOperator relation = feature.Shape as IRelationalOperator;
                    //���ҳ�����ѧ�� ���жϵ�ǰѧУ�����Ƿ������ѧ��
                    ILayer layerhouse = GetLayer("house");
                    IFeatureLayer featureLayerlayerhouse = layerhouse as IFeatureLayer;
                    IFeatureClass featureClasslayerhouse = featureLayerlayerhouse.FeatureClass;
                    IQueryFilter queryFilterlayerhouse = new QueryFilterClass();
                    IFeatureCursor featureCusorlayerhouse;
                    queryFilterlayerhouse.WhereClause = "1=1";
                    featureCusorlayerhouse = featureClasslayerhouse.Search(queryFilterlayerhouse, true);
                    IFeature featurelayerhouse = null;
                    //���ҳ�����ѧ�� ���жϵ�ǰѧУ�����Ƿ������ѧ��
                    List<IFeature> listFeature = new List<IFeature>();

                    DataTable dtTemp = new DataTable();
                    dtTemp.Columns.Add("����");
                    dtTemp.Columns.Add("¥��");
                    dtTemp.Columns.Add("¥��");
                    dtTemp.Columns.Add("���");
                    dtTemp.Columns.Add("����");
                    dtTemp.Columns.Add("��ַ");
                    dtTemp.Columns.Add("�绰");

                    while ((featurelayerhouse = featureCusorlayerhouse.NextFeature()) != null)
                    {

                        if (relation.Contains(featurelayerhouse.Shape))
                        {
                            DataRow dr = dtTemp.NewRow();
                            dr["����"] = GetFeatureValue(featurelayerhouse, "name");
                            dr["¥��"] = GetFeatureValue(featurelayerhouse, "floor");
                            dr["¥��"] = GetFeatureValue(featurelayerhouse, "age");
                            dr["���"] = GetFeatureValue(featurelayerhouse, "area");
                            dr["����"] = GetFeatureValue(featurelayerhouse, "price");
                            dr["��ַ"] = GetFeatureValue(featurelayerhouse, "address");
                            dr["�绰"] = GetFeatureValue(featurelayerhouse, "telephone");
                            dtTemp.Rows.Add(dr);
                            //listFeature.Add(featurelayerhouse);
                        }
                        featurelayerhouse = null;
                    }

                    //Dictionary<string, string> dicFields = new Dictionary<string, string>();
                    //dicFields.Add("name", "����");
                    //dicFields.Add("floor", "¥��");
                    //dicFields.Add("age", "¥��");
                    //dicFields.Add("area", "���");
                    //dicFields.Add("price", "����");
                    //dicFields.Add("address", "��ַ");
                    //dicFields.Add("telephone", "�绰");
                    //ataTable dt = GetDataTable(listFeature, dicFields);
                    ShowDatatable frmShow = new ShowDatatable(dtTemp);
                    frmShow.Size = new Size(800, 600);
                    frmShow.Text = "ѧ���ڵķ���";
                    frmShow.ShowDialog();
                }

            }
        }

        private void �����ܱ�������ʩ��ѯToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectHouse shForm = new SelectHouse();
            shForm.listItem = new List<string>();
            ILayer layer = GetLayer("house");

            IFeatureLayer featureLayer = layer as IFeatureLayer;
            IFeatureClass featureClass = featureLayer.FeatureClass;
            IQueryFilter queryFilter = new QueryFilterClass();
            IFeatureCursor featureCusor;
            queryFilter.WhereClause = "1=1";
            featureCusor = featureClass.Search(queryFilter, true);
            IFeature feature = null;
            int findex = -1;
            //���ҳ����е㲢�������
            while ((feature = featureCusor.NextFeature()) != null)
            {
                if (findex == -1)
                {
                    findex = feature.Fields.FindField("Name");
                }
                shForm.listItem.Add(feature.get_Value(findex).ToString());
            }

            if (shForm.ShowDialog() == DialogResult.OK)
            {
                this.Refresh();
                //MessageBox.Show(shForm.SelectItem);
                //�����߼�

                //���ҳ�����ѧУͼԪ
                queryFilter.WhereClause = String.Format("Name='{0}'", shForm.SelectItem);
                featureCusor = featureClass.Search(queryFilter, true);

                while ((feature = featureCusor.NextFeature()) != null)
                {
                    IPoint point = (feature.Shape as IArea).Centroid;
                    double distance = 0.002;
                    string temp = string.Empty;
                    ILayer BankLayer = GetLayer("bank");
                    temp += "����:" + GetNearPoint(point, BankLayer, distance, "Name")+"\r\n";

                    ILayer MsfwLayer = GetLayer("msfw");
                    temp += "����:" + GetNearPoint(point, MsfwLayer, distance, "Name") + "\r\n";

                    ILayer SyfwLayer = GetLayer("syfw");
                    temp += "����:" + GetNearPoint(point, SyfwLayer, distance, "Name") + "\r\n";

                    ILayer YlwsLayer = GetLayer("ylws");
                    temp += "ҽ������:" + GetNearPoint(point, YlwsLayer, distance, "Name") + "\r\n";
                    //MessageBox.Show(temp);
                    return;
                }
            }
        }

        public string GetNearPoint(IPoint point, ILayer layer, double distance,string FieldName)
        {
            IFeatureLayer featureLayer = layer as IFeatureLayer;
            //��ȡfeatureLayer��featureClass 
            IFeatureClass featureClass = featureLayer.FeatureClass;
            IFeature feature = null;
            IQueryFilter queryFilter = new QueryFilterClass();
            IFeatureCursor featureCusor;
            queryFilter.WhereClause = "1=1";
            featureCusor = featureClass.Search(queryFilter, true);
            double angle = 0;
            string nearName = string.Empty;

            string temp = string.Empty;
            int findex = -1;
            //���ҳ����е㲢�������
            while ((feature = featureCusor.NextFeature()) != null)
            {
                IPoint point2 = feature.Shape as IPoint;

                double tempLength = getDistanceOfTwoPoints(point2.X, point2.Y, point.X, point.Y);
                if (tempLength < distance)
                {
                    //�ڷ�Χ��
                    axMapControl1.FlashShape(feature.Shape); //��˸
                    //if (findex == -1)
                    //{
                    //    findex = feature.Fields.FindField(FieldName);
                    //}
                    //temp += feature.get_Value(findex)+",";
                }
            }
            return temp.TrimEnd(',');
        }
        private double ConvertPixelsToMapUnits(IActiveView pActiveView, double pixelUnits)
        {
            // Uses the ratio of the size of the map in pixels to map units to do the conversion
            IPoint p1 = pActiveView.ScreenDisplay.DisplayTransformation.VisibleBounds.UpperLeft;
            IPoint p2 = pActiveView.ScreenDisplay.DisplayTransformation.VisibleBounds.UpperRight;
            int x1, x2, y1, y2;
            pActiveView.ScreenDisplay.DisplayTransformation.FromMapPoint(p1, out x1, out y1);
            pActiveView.ScreenDisplay.DisplayTransformation.FromMapPoint(p2, out x2, out y2);
            double pixelExtent = x2 - x1;
            double realWorldDisplayExtent = pActiveView.ScreenDisplay.DisplayTransformation.VisibleBounds.Width;
            double sizeOfOnePixel = realWorldDisplayExtent / pixelExtent;
            return pixelUnits * sizeOfOnePixel;
        }

        public double getDistanceOfTwoPoints(double X1, double Y1, double X2, double Y2)
        {
            return Math.Sqrt((X1 - X2) * (X1 - X2) + (Y1 - Y2) * (Y1 - Y2));
            //return ConvertPixelsToMapUnits(axMapControl1.ActiveView,Math.Sqrt((X1 - X2) * (X1 - X2) + (Y1 - Y2) * (Y1 - Y2)));
        }

        private void menuClearRouteLayer_Click(object sender, EventArgs e)
        {
            for (int i = axMapControl1.Map.LayerCount-1; i >=0; i--)
            {
                if (axMapControl1.Map.get_Layer(i) is INALayer)
                {
                    axMapControl1.Map.DeleteLayer(axMapControl1.Map.get_Layer(i));
                }
            }
        }
        
        private DataTable GetDataTable(List<IFeature> listFeature,Dictionary<string,string> fields)
        {
            //Dictionary<string,string> _fields=new Dictionary<string,string>();
            //foreach(string key in fields.Keys)
            //{
            //    if(!_fields.ContainsKey(fields[key]))
            //    {
            //        _fields.Add(fields[key],key);
            //    }
            //}
            DataTable DtResult = new DataTable();
            foreach (string value in fields.Values)
            {
                DtResult.Columns.Add(value);
            }
            foreach (IFeature f in listFeature)
            {
                DataRow dr =DtResult.NewRow();
                foreach(string key in fields.Keys)
                {
                    dr[fields[key]]=GetFeatureValue(f,key);
                }
                
                DtResult.Rows.Add(dr);
            }
            return DtResult;
        }


        private object GetFeatureValue(IFeature f,string fiedName)
        {
            int index= f.Fields.FindField(fiedName);
            return f.get_Value(index);
        }
    }
}