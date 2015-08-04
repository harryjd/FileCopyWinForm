using System;
using System.Configuration;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Text;
using System.Windows.Forms;

namespace FileDuplicateAPP
{
    public partial class MainWnd : Form
    {
        public static int N_MAX_COUNT = 100;
        public long N_TotalFileSize = 0;
        public long N_FileSizeCopyed = 0;
        public int M_TranSize = 67108864;//默认每次读写64MB的大小
        //private int mf_Pos;

        SynchronizationContext m_syncContext = null;
        private Thread m_CopyFileThread;
        private FileStream m_FSRead;  //实例化源文件FileStream类
        private FileStream m_FSWrite;

        public static ArrayList M_FilesSelected = new ArrayList();

        /// <summary>
        /// MainWnd Constructor
        /// </summary>
        public MainWnd()
        {
            InitializeComponent();
            //获取UI线程同步上下文 
            m_syncContext = SynchronizationContext.Current;

            dtp_Start.Value = DateTime.Now.Date.AddDays(-1);
            dtp_End.Value = DateTime.Now.Date.AddDays(1);
            radProgressBar1.Maximum = 100;
            //mf_Pos = 0;
            folderBrowserDialog1.SelectedPath = Application.StartupPath.ToString();
            String strSrcDir =(ConfigurationManager.AppSettings["LastSrcDir"]==null)?Application.StartupPath.ToString():ConfigurationManager.AppSettings["LastSrcDir"].ToString().Trim();
            String strDestDir = (ConfigurationManager.AppSettings["LastDestDir"] == null) ? Application.StartupPath.ToString() : ConfigurationManager.AppSettings["LastDestDir"].ToString().Trim();
            txtBox_SrcDir.Text = strSrcDir;
            txtBox_DestDir.Text = strDestDir;
            if (!String.IsNullOrEmpty(ConfigurationManager.AppSettings["TranSizePerTime"]))
            {
                try
                {
                    M_TranSize = System.Convert.ToInt32(ConfigurationManager.AppSettings["TranSizePerTime"].ToString());
                }
                catch
                {
                    M_TranSize = 67108864;
                }
            }
        }

        /// <summary>
        /// 【复制文件】按钮响应函数
        /// </summary>
        private void radBtn_Duplicate_Click(object sender, EventArgs e)
        {
            radProgressBar1.Visible = true;
            
            //M_FilesSelected.Clear();

            if ((Directory.Exists(txtBox_SrcDir.Text.Trim())) && (Directory.Exists(txtBox_DestDir.Text.Trim())))
            {
                txtBox_Log.AppendText(System.Environment.NewLine +  " ------------------ ");
                txtBox_Log.AppendText(System.Environment.NewLine + "筛选时间范围：" + dtp_Start.Value.ToString() + " - " + dtp_End.Value.ToString());
                txtBox_Log.AppendText(System.Environment.NewLine + "开始时间：" + DateTime.Now.ToString());
                
                //创建复制文件的线程
                m_CopyFileThread = new Thread(new ThreadStart(Hjd_DuplicateFile_ThreadStart));
                m_CopyFileThread.IsBackground = true;
                m_CopyFileThread.Start();
            }
            else
            {
                String strErrorMsg = "源路径或目标路径不存在，请检查后再开始复制。";
                txtBox_Log.AppendText(System.Environment.NewLine + strErrorMsg);
                MessageBox.Show(strErrorMsg, "错误提示", MessageBoxButtons.OK);
            }
        }

        /// <summary>
        /// 遍历指定的目录及子目录记录符合条件的文件
        /// </summary>
        public void HJD_AccessDir(String strDirName)
        {
            System.IO.DirectoryInfo dirInfo_srcDir = new DirectoryInfo(strDirName);
            System.IO.FileInfo[] fileInfo_curDir = dirInfo_srcDir.GetFiles();
            DateTime startDatetime = dtp_Start.Value.Date;
            DateTime stopDatetime = dtp_End.Value.Date;
            
            foreach (FileInfo curFile in fileInfo_curDir)
            {
                if ((curFile.LastWriteTime >= startDatetime) && (curFile.LastWriteTime < stopDatetime))
                {
                    //把在筛选日期范围内的文件信息写入到记录列表中去
                    FileInfo mySelectedFileInfo = curFile;
                    M_FilesSelected.Add(mySelectedFileInfo);
                }
            }

            System.IO.DirectoryInfo[] selectedSubDirs = dirInfo_srcDir.GetDirectories();
            foreach(DirectoryInfo subDir in selectedSubDirs)
            {
                HJD_AccessDir(subDir.FullName);
            }
        }

        /// <summary>
        /// ThreadStart()
        /// </summary>
        public void Hjd_DuplicateFile_ThreadStart()
        {
            String strSrcDir = txtBox_SrcDir.Text.Trim(), strDestDir = txtBox_DestDir.Text.Trim();
            
            Hjd_DuplicateFile(strSrcDir, strDestDir);
            //M_FilesSelected.Clear();
        }

        /// <summary>
        /// 在新线程中执行的函数
        /// </summary>
        public void Hjd_DuplicateFile(String strSrcRootDir, String strDestRootDir)
        {
            //int nCount = 0;
            System.IO.DirectoryInfo dirInfo_srcDir = new DirectoryInfo(strSrcRootDir);
            String strLogMsg = String.Empty;
            //遍历指定的目录及子目录，记录符合条件的文件
            M_FilesSelected.Clear();
            HJD_AccessDir(strSrcRootDir);
            //改变按钮状态
            m_syncContext.Post(ChangeCmdBtnState, String.Empty);
            m_syncContext.Post(ShowLogMessage, "符合条件的文件数：" + Convert.ToString(M_FilesSelected.Count));

            m_syncContext.Post(ClearProgress, String.Empty);

            #region //计算符合条件的文件的总大小
            for (int nPos = 0; nPos < M_FilesSelected.Count; nPos++)
            {
                FileInfo curFileInfo = (FileInfo)M_FilesSelected[nPos];
                N_TotalFileSize += curFileInfo.Length;
            }

            if ((N_TotalFileSize < 1024) && (N_TotalFileSize >= 0))
            {
                strLogMsg = "文件总共大小：" + Convert.ToString(N_TotalFileSize) + "Byte";
            }
            else if ((N_TotalFileSize < (1024 * 1024)) && (N_TotalFileSize >= 1024))
            {
                strLogMsg = "文件总共大小：" + Convert.ToString(N_TotalFileSize / 1024) + "kB";
            }
            else if ((N_TotalFileSize < (1024 * 1024 * 1024)) && (N_TotalFileSize >= (1024 * 1024)))
            {
                strLogMsg = "文件总共大小：" + Convert.ToString(N_TotalFileSize / 1024 / 1024) + "MB";
            }
            else
            {
                strLogMsg = "文件总共大小：" + Convert.ToString(N_TotalFileSize / 1024 / 1024 / 1024) + "GB";
            }
            m_syncContext.Post(ShowLogMessage, strLogMsg); 
            #endregion

            int nSrcDirLen = strSrcRootDir.Length;

            for (int nPos = 0; nPos < M_FilesSelected.Count; nPos++)
            {
                FileInfo curFileInfo = (FileInfo)M_FilesSelected[nPos];
                bool isFileExists = false;

                try
                {
                    #region //处理源根目录下的子目录，生成目标目录
                    String strSrcSubDir = curFileInfo.DirectoryName.Substring(nSrcDirLen);

                    //目标路径及文件名
                    String strDestDirName = strDestRootDir + strSrcSubDir;
                    String strDestFullPathFileName = strDestDirName + @"\" + curFileInfo.Name;
                    if (!Directory.Exists(strDestDirName))
                    {
                        DirectoryInfo myDI = new DirectoryInfo(strDestDirName);
                        myDI.Create();
                        isFileExists = false;
                    }
                    else
                    {
                        //目标路径已经存在就检查当前文件名是否已经在目标路径存在
                        FileInfo myDestFileInfo = new FileInfo(strDestFullPathFileName);
                        isFileExists = myDestFileInfo.Exists;
                    } 
                    #endregion
                    if (isFileExists)
                    {
                        #region 更新进度条
                        N_FileSizeCopyed += curFileInfo.Length;//已经拷贝了的文件大小
                        m_syncContext.Post(ShowProgress, curFileInfo);
                        m_syncContext.Post(ShowLogMessage, curFileInfo.FullName + " 已存在，不需要复制。");
                        #endregion
                    }
                    else
                    {
                        #region 需要拷贝文件的情况
                        m_FSRead = new FileStream(curFileInfo.FullName, FileMode.Open, FileAccess.Read);//以只读方式打开源文件
                        FileStream fileToCreate = new FileStream(strDestFullPathFileName, FileMode.Create); //创建目的文件，如果已存在将被覆盖
                        fileToCreate.Close();//关闭所有fileToCreate的资源
                        fileToCreate.Dispose();//释放所有fileToCreate的资源
                        m_syncContext.Post(ShowLogMessage, curFileInfo.FullName + " 开始复制。");
                        m_FSWrite = new FileStream(strDestFullPathFileName, FileMode.Append, FileAccess.Write);//以写方式打开目的文件
                        //根据一次传输的大小，计算最大传输个数. Math.Ceiling 方法 (Double),返回大于或等于指定的双精度浮点数的最小整数值。
                        int nMaxReadCount = Convert.ToInt32(Math.Ceiling((double)(m_FSRead.Length / M_TranSize)));
                        
                        int FileSize; //每次要拷贝的文件的大小
                        if (M_TranSize < m_FSRead.Length)  //如果分段拷贝，即每次拷贝内容小于文件总长度
                        {
                            #region 读取文件内容到缓冲区数组，然后写入到目标文件
                            byte[] buffer = new byte[M_TranSize]; //根据传输的大小，定义一个字节数组，用来存储传输的字节
                            long nCopied = 0;//记录传输的大小
                            //int tem_n = 1;//设置进度栏中进度的增加个数
                            while (nCopied <= (m_FSRead.Length - M_TranSize))
                            {
                                FileSize = m_FSRead.Read(buffer, 0, M_TranSize);//从0开始读到buffer字节数组中，每次最大读M_TranSize
                                m_FSRead.Flush();//清空缓存
                                m_FSWrite.Write(buffer, 0, M_TranSize); //向目的文件写入字节
                                m_FSWrite.Flush();//清空缓存
                                m_FSWrite.Position = m_FSRead.Position; //使源文件和目的文件流的位置相同
                                nCopied += FileSize; //记录已经拷贝的大小
                                
                                #region 更新进度条
                                N_FileSizeCopyed += FileSize;//已经拷贝了的文件大小
                                m_syncContext.Post(ShowProgress, curFileInfo);
                                #endregion
                            }
                            int leftSize = (int)(m_FSRead.Length - nCopied); //获取剩余文件的大小
                            FileSize = m_FSRead.Read(buffer, 0, leftSize); //读取剩余的字节
                            m_FSRead.Flush();
                            m_FSWrite.Write(buffer, 0, leftSize); //写入剩余的部分
                            m_FSWrite.Flush(); 
                            #endregion
                            #region 更新进度条
                            N_FileSizeCopyed += leftSize;//已经拷贝了的文件大小
                            m_syncContext.Post(ShowProgressAndLog, curFileInfo);
                            #endregion
                            
                        }
                        else //如果整体拷贝，即每次拷贝内容大于文件总长度
                        {
                            byte[] buffer = new byte[m_FSRead.Length];
                            m_FSRead.Read(buffer, 0, (int)m_FSRead.Length);
                            m_FSRead.Flush();
                            m_FSWrite.Write(buffer, 0, (int)m_FSRead.Length);
                            m_FSWrite.Flush();
                            #region 更新进度条
                            N_FileSizeCopyed += curFileInfo.Length;//已经拷贝了的文件大小
                            m_syncContext.Post(ShowProgressAndLog, curFileInfo);
                            #endregion
                        }
                        m_FSRead.Close();
                        m_FSWrite.Close(); 
                        #endregion
                    }
                }
                catch (Exception ex)
                {
                    m_syncContext.Post(ShowLogMessage, curFileInfo.Name + " 复制出现错误，错误信息：" + ex.Message);
                }
            }//end of for
            m_syncContext.Post(ChangeCmdBtnState, String.Empty);
            m_syncContext.Post(ShowLogMessage, "已完成，完成时间：" + DateTime.Now.ToString());
            MessageBox.Show("已完成文件复制.", "正常提示", MessageBoxButtons.OK);
            m_syncContext.Post(ClearProgress, String.Empty);
        }

        /// <summary>
        /// 刷新界面
        /// </summary>
        public void ShowProgress(object eventArgs)
        {
            //从不是创建控件的线程去访问该控件，是不被允许的。
            radProgressBar1.Value1 = (int)(100 * N_FileSizeCopyed / N_TotalFileSize);
            //txtBox_Log.AppendText(System.Environment.NewLine + ((FileInfo)eventArgs).FullName + " been copied...");
            txtBox_Log.Focus();
        }

        public void ShowProgressAndLog(object eventArgs)
        {
            //从不是创建控件的线程去访问该控件，是不被允许的。
            radProgressBar1.Value1 = (int)(100 * N_FileSizeCopyed / N_TotalFileSize);
            txtBox_Log.AppendText(System.Environment.NewLine + ((FileInfo)eventArgs).FullName + " 复制完成。");
            txtBox_Log.Focus();
        }

        public void ClearProgress(object eventArgs)
        {
            radProgressBar1.Value1 = 0;
        }

        public void ShowLogMessage(object eventMsg)
        {
            txtBox_Log.AppendText(System.Environment.NewLine + (String)eventMsg);
            txtBox_Log.Focus();
        }

        public void ChangeCmdBtnState(object eventArgs)
        {
            radBtn_Duplicate.Enabled = !radBtn_Duplicate.Enabled;
        }

        /// <summary>
        /// 
        /// </summary>
        private void btn_OpenSrcDir_Click(object sender, EventArgs e)
        {
            DialogResult myDR = folderBrowserDialog1.ShowDialog();
            if (myDR.Equals(DialogResult.OK) || (myDR.Equals(DialogResult.Yes)))
            {
                txtBox_SrcDir.Text = folderBrowserDialog1.SelectedPath;
                //Configuration cfa = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                //cfa.AppSettings.Settings["LastSrcDir"].Value = txtBox_SrcDir.Text.Trim();
                //cfa.Save();
            }
        }

        private void btn_OpenDestDir_Click(object sender, EventArgs e)
        {
            DialogResult myDR = folderBrowserDialog2.ShowDialog();
            if (myDR.Equals(DialogResult.OK) || (myDR.Equals(DialogResult.Yes)))
            {
                txtBox_DestDir.Text = folderBrowserDialog2.SelectedPath;

                //Configuration cfa = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                //cfa.AppSettings.Settings["LastDestDir"].Value = txtBox_DestDir.Text.Trim();
                //cfa.Save();
            }
        }
        
        private void MainWnd_FormClosing(object sender, FormClosingEventArgs e)
        {
            Configuration cfa = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            cfa.AppSettings.Settings["LastSrcDir"].Value = txtBox_SrcDir.Text.Trim();
            cfa.AppSettings.Settings["LastDestDir"].Value = txtBox_DestDir.Text.Trim();
            cfa.Save();
        }
    }

    //public delegate void TakesAWhileDelegate(String strSrcDir, String strDestDir);
    public class HJD_FILE_DUPLICATION_INFO
    {
        public FileInfo oFileInfo;
        public long fFileSize;
    }
}
