﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using EPDM.Interop.epdm;
using ExportSLDPRTToDXF.Models;
using EPDM.Interop.EPDMResultCode;
using Patterns.Observer;
using System.Windows.Forms;

namespace ExportSLDPRTToDXF
{
    public class SolidWorksPdmAdapter 
    {         
        public SolidWorksPdmAdapter() : base()
        {
            edmVeult8 = (IEdmVault8)PdmExemplar;
        }

        private string vaultName = "Vents-PDM";
        private int BOM_ID = 22;
        /// <summary>
        /// PDM exemplar.
        /// </summary>
        private static IEdmVault5 edmVeult5 = null;
        private static IEdmVault8 edmVeult8;
        private static EdmViewInfo[] Views = null;

        public void AuthoLogin (string vaultName)
        {
            this.vaultName = vaultName;


            if (!PdmExemplar.IsLoggedIn)
            {
                edmVeult5.LoginAuto(this.vaultName, 0); 
            } 
        }
        /// <summary>
        /// Returns exemplar pdm SolidWorks 
        /// </summary>
        private IEdmVault5 PdmExemplar
        {
            get
            {
                try
                {
                    if (edmVeult5 == null)
                    {
                        KillProcsses("ViewServer");
                        KillProcsses("AddInSrv");

                        edmVeult5 = new EdmVault5();                       
                    }
                    return edmVeult5;
                }
                catch (Exception exception)
                {
                    MessageObserver.Instance.SetMessage("Невозможно создать экземпляр Vents-PDM - " + vaultName + "\n" + exception);
                    return null;
                }

            }
        }
        /// <summary>
        /// Search document by name and returns colection the FileModelPdm .
        /// </summary>
        /// <param name="segmentName"></param>
        /// <returns></returns>
        public IEnumerable <FileModelPdm> SearchDoc(string segmentName)
        { 
            List<FileModelPdm> searchResult = new List<FileModelPdm>();
            try
            {
                var Search = (PdmExemplar as IEdmVault7).CreateUtility(EdmUtility.EdmUtil_Search);
                Search.FileName = segmentName;
                Search.SetToken(EdmSearchToken.Edmstok_FindFolders, false);
                int count = 0;

                IEdmSearchResult5 Result = Search.GetFirstResult();
                while (Result != null)
                {                    
                    searchResult.Add(new FileModelPdm
                    {
                        FileName = Result.Name,
                        IDPdm = Result.ID,
                        FolderId = Result.ParentFolderID,
                        Path = Result.Path,
                        FolderPath = Path.GetDirectoryName(Result.Path),
                        CurrentVersion = Result.Version
                    });

                    Result = Search.GetNextResult();
                    count++;
                }    

                MessageObserver.Instance.SetMessage ("Поиск успешно завершон, найдено объектов " + searchResult.Count + " по запросу " + segmentName);
            }
            catch (Exception exception)
            {
                MessageObserver.Instance.SetMessage("Поиск по запросу "+ segmentName+" завершон c ошибкой: "+ exception.ToString() + " в связи с чем возвращена пустая колекция FileModelPdm");
                throw exception;
            }
            return searchResult.Where(each => Path.GetExtension( each.FileName).ToUpper() == ".SLDPRT" || Path.GetExtension(each.FileName).ToUpper( ) == ".SLDASM");
        }

        /// <summary>
        /// Download file in to local directory witch has fixed path
        /// </summary>
        /// <param name="fileModel"></param>
        public void DownLoadFile(FileModelPdm fileModel)
        {            
            try
            {
                var batchGetter = (IEdmBatchGet)(PdmExemplar as IEdmVault7).CreateUtility(EdmUtility.EdmUtil_BatchGet);
                batchGetter.AddSelectionEx((EdmVault5)PdmExemplar, fileModel.IDPdm, fileModel.FolderId, 0);
                if ((batchGetter != null))
                {
                    batchGetter.CreateTree(0, (int)EdmGetCmdFlags.Egcf_SkipUnlockedWritable);
                    batchGetter.GetFiles(0, null);
                }
                MessageObserver.Instance.SetMessage("Файл " + fileModel.FileName + " с id " + fileModel.IDPdm + " получен локально. путь:" + fileModel.Path);
            }
            catch (Exception exception)
            {
                MessageObserver.Instance.SetMessage("Неудалось получить файл " + fileModel.FileName + " с id " + fileModel.IDPdm + "; путь:" + fileModel.Path);
                throw exception;

            }
        }

        /// <summary>
        ///  Get configuration by data model
        /// </summary>
        /// <param name="fileModel"></param>
        /// <returns></returns>
        public string[] GetConfigigurations(string filePath)
        {
            IEdmFolder5 ppoRetParentFolder;
            IEdmFile9 fileModelInfo = PdmExemplar.GetFileFromPath(filePath, out ppoRetParentFolder) as IEdmFile9;
            EdmStrLst5 cfgList = null;
            cfgList = fileModelInfo.GetConfigurations( );
            IEdmPos5 edmPos;
            edmPos = cfgList.GetHeadPosition( );

            #region fill resault array
            List<string> configurationResaultArray = new List<string>( );
            int id = 0;
            while (!edmPos.IsNull)
            {
                string buff = cfgList.GetNext(edmPos);
                if (buff.CompareTo("@") != 0)
                {
                    configurationResaultArray.Add(buff);
                }
                id++;
            }
            #endregion

            return configurationResaultArray.ToArray( );
        }
        public void GetLastVersionAsmPdm(string path)
        {
            try
            {
                IEdmFolder5 oFolder;
                //GetEdmFile5(path, out oFolder).GetFileCopy(0, 0, oFolder.ID, (int)EdmGetFlag.EdmGet_RefsVerLatest);
            }
            catch (Exception exception)
            {
               MessageObserver.Instance.SetMessage("Got last version for file by path" + path + "\n" + exception.ToString());
            }
        }

        internal IEdmFile5 GetEdmFile5(string path)
        {
            try
            {
                IEdmFolder5 oFolder;
                var edmFile5 = PdmExemplar.GetFileFromPath(path, out oFolder);
                edmFile5.GetFileCopy(1, 0, oFolder.ID, (int)EdmGetFlag.EdmGet_RefsVerLatest);
                return edmFile5;
            }
            catch (Exception exception)
            {
                //Логгер.Ошибка($"Message - {exception.ToString()}\nPath - {path}\nStackTrace - {exception.StackTrace}", null, "GetEdmFile5", "SwEpdm");
                throw exception;
            }
        } 
        private void KillProcsses (string name)
        {
            var processes = System.Diagnostics.Process.GetProcessesByName(name );
            foreach (var process in processes)
            {               
                process.Kill();
                //Console.WriteLine("\nFind proccess and kill: " + process);
            }
          
        }
   
        #region bom
        public IEnumerable<BomShell> GetBomShell(string filePath, string bomConfiguration )
        {

            try
            {
                IEdmFolder5 oFolder;

                IEdmFile7 EdmFile7 = (IEdmFile7)PdmExemplar.GetFileFromPath(filePath, out oFolder);
                var bomView = EdmFile7.GetComputedBOM(22, -1, bomConfiguration, (int)EdmBomFlag.EdmBf_ShowSelected);

                if (bomView == null)
                    throw new Exception("Computed BOM it can not be null");
                object[] bomRows;
                EdmBomColumn[] bomColumns;
                bomView.GetRows(out bomRows);
                bomView.GetColumns(out bomColumns);


                var bomTable = new DataTable();

                string str = "";
                foreach (EdmBomColumn bomColumn in bomColumns)
                {
                    str += bomColumn.mbsCaption + " ";
                    bomTable.Columns.Add(new DataColumn { ColumnName = bomColumn.mbsCaption });
                }

                //bomTable.Columns.Add(new DataColumn { ColumnName = "Путь" });
                //bomTable.Columns.Add(new DataColumn { ColumnName = "Уровень" });
                //bomTable.Columns.Add(new DataColumn { ColumnName = "КонфГлавнойСборки" });
                //bomTable.Columns.Add(new DataColumn { ColumnName = "ТипОбъекта" });
                //bomTable.Columns.Add(new DataColumn { ColumnName = "GetPathName" });

                for (var i = 0; i < bomRows.Length; i++)
                {
                    var cell = (IEdmBomCell)bomRows.GetValue(i);

                    bomTable.Rows.Add( );

                    for (var j = 0; j < bomColumns.Length; j++)
                    {
                        var column = (EdmBomColumn)bomColumns.GetValue(j);

                        object value;
                        object computedValue;
                        string config;
                        bool readOnly;

                        cell.GetVar(column.mlVariableID, column.meType, out value, out computedValue, out config, out readOnly);

                        if (value != null)
                        {
                            bomTable.Rows[i][j] = value;
                        }
                        else
                        {
                            bomTable.Rows[i][j] = null;
                        }
                       // bomTable.Rows[i][j + 1] = cell.GetTreeLevel( );

                      //  bomTable.Rows[i][j + 2] = bomConfiguration;
                      //  bomTable.Rows[i][j + 3] = config;
                      //  bomTable.Rows[i][j + 4] = cell.GetPathName( );
                    }
                }

                return BomTableToBomList(bomTable);

            }
            catch (COMException ex)
            {
                MessageBox.Show("Failed get bom shell " + (EdmResultErrorCodes_e)ex.ErrorCode);

                throw  ex;
                return null;
            }

        }
        #endregion

        private   IEnumerable<BomShell> BomTableToBomList(DataTable table)
        {
            List<BomShell> BoomShellList = new List<BomShell>(table.Rows.Count);
            try
            {
                BoomShellList.AddRange(from DataRow row in table.Rows
                                       select row.ItemArray into values
                                       select new BomShell
                                       {
                                           PartNumber = values[0].ToString( ),
                                           Description = values[1].ToString( ),
                                           FolderPath = values[2].ToString( ),
                                           IdPdm = Convert.ToInt32(values[3]),
                                           Configuration = values[4].ToString( ),
                                           Version = Convert.ToInt32(values[5])      
                                           , FileName = values[6].ToString( )

                                       });
            }
            catch(Exception exception)
            {
                MessageObserver.Instance.SetMessage("Failed get bom shell list: " + exception.ToString());
            }

            //LoggerInfo("Список из полученой таблицы успешно заполнен элементами в количестве" + bomList.Count);
            return BoomShellList;
        }

        public void CheckInOutPdm (IEnumerable<string>   pathToFiles, bool registration)
        {
            foreach (var eachFile in pathToFiles)
            {
                CheckInOutPdm(eachFile,registration);
            }
        }

        /// <summary>
        /// Registration or unregistation files by their paths and registration status.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="isRegistration"></param>
        public void CheckInOutPdm(string pathToFile, bool registration)
        {
            #region not working code
            //foreach (var file in filesList)
            //{
            //    try
            //    {
            //        IEdmFolder5 oFolder;
            //        IEdmFile5 edmFile5 = edmVault5.GetFileFromPath(file.FullName, out oFolder);

            //        var batchGetter = (IEdmBatchGet)(edmVault5 as IEdmVault7).CreateUtility(EdmUtility.EdmUtil_BatchGet);
            //        batchGetter.AddSelectionEx(edmVault5, edmFile5.ID, oFolder.ID, 0);
            //        if ((batchGetter != null))
            //        {
            //            batchGetter.CreateTree(0, (int)EdmGetCmdFlags.Egcf_SkipUnlockedWritable);
            //            batchGetter.GetFiles(0, null);
            //        }

            //        // Разрегистрировать
            //        if (!registration)
            //        {                    

            //            if (!edmFile5.IsLocked)
            //            {


            //                edmFile5.LockFile(oFolder.ID, 0);
            //                Thread.Sleep(50);
            //            }
            //        }
            //        else if (registration)
            //            if (edmFile5.IsLocked)
            //            {
            //                edmFile5.UnlockFile(oFolder.ID, "");
            //                Thread.Sleep(50);
            //            }
            //    }

            //    catch (Exception exception)
            //    {
            //       //Observer.MessageObserver.Instance.SetMessage(exception.ToString() + "\n" + exception.StackTrace + "\n" + exception.Source);
            //    }
            //}



            //foreach (var eachFile in files)
            //{
            //    try
            //    {
            //        IEdmFolder5 folderPdm;
            //        IEdmFile5 filePdm = edmVault5.GetFileFromPath(eachFile.FullName, out folderPdm);

            //        if (filePdm == null)
            //        {
            //            filePdm.GetFileCopy(0, 0, folderPdm.ID, (int)EdmGetFlag.EdmGet_Simple);
            //        }

            //        // Разрегистрировать
            //        if (registration == false)
            //        {
            //            if (!filePdm.IsLocked)
            //            {
            //                filePdm.LockFile(folderPdm.ID, 0);
            //                Thread.Sleep(50);
            //            }
            //        }
            //        else
            //        {
            //            if (filePdm.IsLocked)
            //            {
            //                filePdm.UnlockFile(folderPdm.ID, "");
            //                Thread.Sleep(50);
            //            }
            //        }
            //    }

            //    catch (Exception exception)
            //    {
            //       //Observer.MessageObserver.Instance.SetMessage(exception.ToString() + "\n" + exception.StackTrace + "\n" + exception.Source);
            //    }
            //}

            #endregion


            //Console.WriteLine(pathToFile);
            var retryCount = 2;
            var success = false;
            while (!success && retryCount > 0)
            {
                try
                {
                    IEdmFolder5 oFolder;
                    IEdmFile5 edmFile5 = PdmExemplar.GetFileFromPath(pathToFile, out oFolder);
                    edmFile5.GetFileCopy(0, 0, oFolder.ID, (int)EdmGetFlag.EdmGet_Simple);
                    // Разрегистрировать
                    if (registration == false)
                    {

                        m1:
                        edmFile5.LockFile(oFolder.ID, 0);
                        //Observer.MessageObserver.Instance.SetMessage(edmFile5.Name);
                        Thread.Sleep(50);
                        var j = 0;
                        if (!edmFile5.IsLocked)
                        {
                            j++;
                            if (j > 5)
                            {
                                goto m3;
                            }
                            goto m1;
                        }
                    }
                    // Зарегистрировать
                    if (registration)
                    {
                        try
                        {
                            m2:
                            edmFile5.UnlockFile(oFolder.ID, "");
                            Thread.Sleep(50);
                            var i = 0;
                            if (edmFile5.IsLocked)
                            {
                                i++;
                                if (i > 5)
                                {
                                    goto m4;
                                }
                                goto m2;
                            }
                        }
                        catch (Exception exception)
                        {
                            MessageObserver.Instance.SetMessage(exception.ToString());
                        }
                    }
                    m3:
                    m4:
                    //LoggerInfo(string.Format("В базе PDM - {1}, зарегестрирован документ по пути {0}", file.FullName, vaultName), "", "CheckInOutPdm");
                    success = true;
                }


                catch (Exception exception)
                {
                    //  Логгер.Ошибка($"Message - {exception.ToString()}\nfile.FullName - {file.FullName}\nStackTrace - {exception.StackTrace}", null, "CheckInOutPdm", "SwEpdm");
                    retryCount--;
                    Thread.Sleep(200);
                    if (retryCount == 0)
                    {
                        //
                    }
                    throw exception;
                }
            }
            if (!success)
            {
                //LoggerError($"Во время регистрации документа по пути {file.FullName} возникла ошибка\nБаза - {vaultName}. {exception.ToString()}", "", "CheckInOutPdm");
            }

        }

        /// <summary>
        /// Adds file to pdm. File must the locate in local directory pdm.
        /// </summary>
        /// <param name="pathToFile"></param>
        /// <param name="folder"></param>
        public string AddToPdm(string pathToFile, string folder)
        {
            
           
            try
            {
                //if (File.Exists(pathToFile))
                //{
                //   File.SetAttributes(pathToFile, FileAttributes.Normal);
                //    File.Delete(pathToFile);
                //}
                var edmFolder = PdmExemplar.GetFolderFromPath(folder);
               
                edmFolder.AddFile(0, pathToFile);

             

            //    Logger.ToLog("Файлы добавлены в PDM");

             return   Path.Combine(folder, Path.GetFileName(pathToFile));

            }
            catch (COMException ex)
            {
                //  Logger.ToLog("ERROR BatchAddFiles " + msg + ", file: " + fileNameErr + " HRESULT = 0x" + ex.ErrorCode.ToString("X") + " " + ex.ToString());   
                throw ex;           
            }
        }

        public void SetVariable(FileModelPdm fileModel, string pathToTempPdf)
        {
            try
            {
                var filePath = fileModel.FolderPath + "\\" + pathToTempPdf;
                IEdmFolder5 folder;
                var aFile = PdmExemplar.GetFileFromPath(filePath, out folder);
                var pEnumVar = (IEdmEnumeratorVariable8)aFile.GetEnumeratorVariable(); ;
                pEnumVar.SetVar("Revision", "", fileModel.CurrentVersion); 
                pEnumVar.CloseFile(true);
            }
            catch (Exception ex)
            { 
                throw ex;
            }
        }

        /// <summary>
        /// Returns FileModelPdm by file id 
        /// </summary>
        /// <param name="fileId">Id in pdm</param>
        /// <param name="isDownload">It allows download file in local</param>
        /// <returns></returns>
        //public FileModelPdm GetFileById(int fileId, bool isDownload = false)
        //{
        //    try
        //    {
        //        IEdmFile5 pdmFile ;
        //        IEdmFolder5 folder;
                
        //             pdmFile = (IEdmFile5)PdmExemplar.GetObject(EdmObjectType.EdmObject_File, fileId);

        //        ////Console.WriteLine(SearchDoc(pdmFile.Name).Count());
        //        FileModelPdm fileModel =  SearchDoc(pdmFile.Name).First(); 
        //            //new FileModelPdm
        //            //{
        //            //    CurrentVersion = pdmFile.CurrentVersion,
        //            //    IDPdm = pdmFile.ID,
        //            //    FileName = pdmFile.Name,
        //            //    //  FolderId = folder.ID,
        //            //    //  FolderPath = folder.LocalPath

        //            //    Path = pdmFile.LockFile
                      
        //            //};


        //        //Console.WriteLine("\n\t\t debug: получен интерфейс файла: " +fileModel.FileName + " id" + fileModel.IDPdm + ", folder path " + fileModel.FolderPath + "\n");
        //        if (isDownload)
        //        {                
        //            DownLoadFile(fileModel);   
        //        }
        //        MessageObserver.Instance.SetMessage("Получен файл " + fileModel.FileName + " по id " + fileId );
        //        return fileModel;
        //    }
        //    catch (Exception exception)
        //    {
        //        MessageObserver.Instance.SetMessage("Неудалось получить файл с id " + fileId +  " " + exception);
        //        throw exception; 
        //    }
        //}

        public EdmBomLayout[] GetBoom(string vaultName)
        {
            IEdmVault7 vault2 = null;
            vault2 = (IEdmVault9)edmVeult5;
            if (!edmVeult5.IsLoggedIn)
            {
                edmVeult5.LoginAuto(vaultName, 0);
            }
            IEdmBomMgr bomMgr = (IEdmBomMgr)vault2.CreateUtility(EdmUtility.EdmUtil_BomMgr);
            EdmBomLayout[] ppoRetLayouts = null; 
            bomMgr.GetBomLayouts(out ppoRetLayouts);
            return ppoRetLayouts;
        }
        public EdmViewInfo[] GetVaultViews()
        {
            EdmViewInfo[] edmViewInfo;
            edmVeult8.GetVaultViews(out edmViewInfo, false);
            return edmViewInfo;
        }
    }

    
}
