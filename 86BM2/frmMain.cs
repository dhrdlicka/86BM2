﻿using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using static _86BM2.VMManager;

namespace _86BM2
{
    public partial class frmMain : Form
    {
        //Win32 API imports
        //Posts a message to the window with specified handle - DOES NOT WAIT FOR THE RECIPIENT TO PROCESS THE MESSAGE!!!
        [DllImport("user32.dll")]
        public static extern int PostMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

        //Focus a window
        [DllImport("user32.dll")]
        public static extern int SetForegroundWindow(IntPtr hwnd);

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        private static RegistryKey regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box", true); //Registry key for accessing the settings and VM list
        public string exepath = ""; //Path to 86box.exe and the romset
        public string cfgpath = ""; //Path to the virtual machines folder (configs, nvrs, etc.)
        public string handle = "";  //Window handle of this window  
        private bool minimize = false; //Minimize the main window when a VM is started?
        private bool showConsole = true; //Show the console window when a VM is started?
        private bool minimizeTray = false; //Minimize the Manager window to tray icon?
        private bool closeTray = false; //Close the Manager Window to tray icon?
        private const string ZEROID = "0000000000000000"; //Used for the id parameter of 86Box -H
        private int sortColumn = 0; //The column for sorting
        private SortOrder sortOrder = SortOrder.Ascending; //Sorting order
        private int launchTimeout = 5000; //Timeout for waiting for 86Box.exe to initialize
        private bool logging = false; //Logging enabled for 86Box.exe (-L parameter)?
        private string logpath = ""; //Path to log file
        private bool gridlines = false; //Are grid lines enabled for VM list?

        public frmMain()
        {
            InitializeComponent();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            /*LoadSettings();
            LoadVMs();

            //Load main window's state, size and position
            WindowState = Settings.Default.WindowState;
            Size = Settings.Default.WindowSize;
            Location = Settings.Default.WindowPosition;

            //Load listview column widths
            clmName.Width = Settings.Default.NameColWidth;
            clmStatus.Width = Settings.Default.StatusColWidth;
            clmDesc.Width = Settings.Default.DescColWidth;
            clmPath.Width = Settings.Default.PathColWidth;
            */

            //Convert the current window handle to a form that's expected by 86Box
            handle = string.Format("{0:X}", Handle.ToInt64());
            handle = handle.PadLeft(16, '0');

            //Check if command line arguments for starting a VM are OK
            if (Program.args.Length == 3 && Program.args[1] == "-S" && Program.args[2] != null)
            {
                //Find the VM with given name
                VirtualMachine vm = Get(Guid.Parse(Program.args[2]));

                //Then select and start it if it's found
                if (vm != null)
                {
                    vm.Start();   
                }
                else
                {
                    MessageBox.Show("The virtual machine with the GUID \"" + Program.args[2] + "\" could not be found. It may have been removed or the specified GUID is incorrect.", "Virtual machine not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnStartStop_Click(object sender, EventArgs e)
        {
            foreach(ListViewItem lvi in lstVMs.SelectedItems)
            {
                VirtualMachine vm = Get((Guid)lvi.Tag);
                if (vm.State == VMState.Stopped)
                    vm.Start();
                else if (vm.State == VMState.Running)
                    vm.RequestStop();
                else if(vm.State == VMState.Paused)
                {
                    vm.Resume();
                    vm.RequestStop();
                }
            }

            VMCountRefresh();
        }

        private void btnConfigure_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem lvi in lstVMs.SelectedItems)
            {
                VirtualMachine vm = Get((Guid)lvi.Tag);
                if (vm.State == VMState.Running || vm.State == VMState.Paused || vm.State == VMState.Stopped)
                    vm.Configure();
            }

            VMCountRefresh();
        }

        private void btnSettings_Click(object sender, EventArgs e)
        {
            dlgSettings dlg = new dlgSettings();
            dlg.ShowDialog();
            LoadSettings(); //Reload the settings due to potential changes    
            dlg.Dispose();
        }

        private void lstVMs_SelectedIndexChanged(object sender, EventArgs e)
        {
            //Disable relevant buttons if no VM is selected
            if (lstVMs.SelectedItems.Count == 0)
            {
                btnConfigure.Enabled = false;
                btnStartStop.Enabled = false;
                btnEdit.Enabled = false;
                btnDelete.Enabled = false;
                btnReset.Enabled = false;
                btnCtrlAltDel.Enabled = false;
                btnPauseResume.Enabled = false;
            }
            else if (lstVMs.SelectedItems.Count == 1)
            {
                //Disable relevant buttons if VM is running
                VirtualMachine vm = Get((Guid)lstVMs.SelectedItems[0].Tag);
                if (vm.State == VMState.Running)
                {
                    btnStartStop.Enabled = true;
                    btnStartStop.Text = "Stop";
                    toolTip.SetToolTip(btnStartStop, "Stop this virtual machine");
                    btnEdit.Enabled = false;
                    btnDelete.Enabled = false;
                    btnConfigure.Enabled = true;
                    btnPauseResume.Enabled = true;
                    btnPauseResume.Text = "Pause";
                    btnReset.Enabled = true;
                    btnCtrlAltDel.Enabled = true;
                }
                else if (vm.State == VMState.Stopped)
                {
                    btnStartStop.Enabled = true;
                    btnStartStop.Text = "Start";
                    toolTip.SetToolTip(btnStartStop, "Start this virtual machine");
                    btnEdit.Enabled = true;
                    btnDelete.Enabled = true;
                    btnConfigure.Enabled = true;
                    btnPauseResume.Enabled = false;
                    btnPauseResume.Text = "Pause";
                    btnReset.Enabled = false;
                    btnCtrlAltDel.Enabled = false;
                }
                else if (vm.State == VMState.Paused)
                {
                    btnStartStop.Enabled = true;
                    btnStartStop.Text = "Stop";
                    toolTip.SetToolTip(btnStartStop, "Stop this virtual machine");
                    btnEdit.Enabled = false;
                    btnDelete.Enabled = false;
                    btnConfigure.Enabled = true;
                    btnPauseResume.Enabled = true;
                    btnPauseResume.Text = "Resume";
                    btnReset.Enabled = true;
                    btnCtrlAltDel.Enabled = true;
                }
                else if (vm.State == VMState.Waiting)
                {
                    btnStartStop.Enabled = false;
                    btnStartStop.Text = "Stop";
                    toolTip.SetToolTip(btnStartStop, "Stop this virtual machine");
                    btnEdit.Enabled = false;
                    btnDelete.Enabled = false;
                    btnReset.Enabled = false;
                    btnCtrlAltDel.Enabled = false;
                    btnPauseResume.Enabled = false;
                    btnPauseResume.Text = "Pause";
                    btnConfigure.Enabled = false;
                }
            }
            else
            {
                btnConfigure.Enabled = false;
                btnStartStop.Enabled = false;
                btnEdit.Enabled = false;
                btnDelete.Enabled = true;
                btnReset.Enabled = false;
                btnCtrlAltDel.Enabled = false;
                btnPauseResume.Enabled = false;
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            dlgAddVM dlg = new dlgAddVM();
            dlg.ShowDialog();
            dlg.Dispose();
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            dlgEditVM dlg = new dlgEditVM();
            dlg.ShowDialog();
            dlg.Dispose();
        }

        //Load the settings from the registry
        private void LoadSettings()
        {
            /*regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box", true);

            //Try to load the settings from registry, if it fails fallback to default values
            try
            {
                exepath = regkey.GetValue("EXEdir").ToString();
                cfgpath = regkey.GetValue("CFGdir").ToString();
                minimize = Convert.ToBoolean(regkey.GetValue("MinimizeOnVMStart"));
                showConsole = Convert.ToBoolean(regkey.GetValue("ShowConsole"));
                minimizeTray = Convert.ToBoolean(regkey.GetValue("MinimizeToTray"));
                closeTray = Convert.ToBoolean(regkey.GetValue("CloseToTray"));
                launchTimeout = (int)regkey.GetValue("LaunchTimeout");
                logpath = regkey.GetValue("LogPath").ToString();
                logging = Convert.ToBoolean(regkey.GetValue("EnableLogging"));
                gridlines = Convert.ToBoolean(regkey.GetValue("EnableGridLines"));
                sortColumn = (int)regkey.GetValue("SortColumn");
                sortOrder = (SortOrder)regkey.GetValue("SortOrder");

                lstVMs.GridLines = gridlines;
                VMSort(sortColumn, sortOrder);
            }
            catch (Exception ex)
            {
                MessageBox.Show("86Box Manager settings could not be loaded. This is normal if you're running 86Box Manager for the first time. Default values will be used.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                //If the key doesn't exist, create it and then reopen it
                if (regkey == null)
                {
                    Registry.CurrentUser.CreateSubKey(@"SOFTWARE\86Box");
                    regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box", true);
                    regkey.CreateSubKey("Virtual Machines");
                }

                cfgpath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\86Box VMs\";
                exepath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\86Box\";
                minimize = false;
                showConsole = true;
                minimizeTray = false;
                closeTray = false;
                launchTimeout = 5000;
                logging = false;
                logpath = "";
                gridlines = false;
                sortColumn = 0;
                sortOrder = SortOrder.Ascending;

                lstVMs.GridLines = false;
                VMSort(sortColumn, sortOrder);

                //Defaults must also be written to the registry
                regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box", true);
                regkey.SetValue("EXEdir", exepath, RegistryValueKind.String);
                regkey.SetValue("CFGdir", cfgpath, RegistryValueKind.String);
                regkey.SetValue("MinimizeOnVMStart", minimize, RegistryValueKind.DWord);
                regkey.SetValue("ShowConsole", showConsole, RegistryValueKind.DWord);
                regkey.SetValue("MinimizeToTray", minimizeTray, RegistryValueKind.DWord);
                regkey.SetValue("CloseToTray", closeTray, RegistryValueKind.DWord);
                regkey.SetValue("LaunchTimeout", launchTimeout, RegistryValueKind.DWord);
                regkey.SetValue("EnableLogging", logging, RegistryValueKind.DWord);
                regkey.SetValue("LogPath", logpath, RegistryValueKind.String);
                regkey.SetValue("EnableGridLines", gridlines, RegistryValueKind.DWord);
                regkey.SetValue("SortColumn", sortColumn, RegistryValueKind.DWord);
                regkey.SetValue("SortOrder", sortOrder, RegistryValueKind.DWord);
            }
            finally
            {
                //To make sure there's a trailing backslash at the end, as other code using these strings expects it!
                if (!exepath.EndsWith(@"\"))
                {
                    exepath += @"\";
                }
                if (!cfgpath.EndsWith(@"\"))
                {
                    cfgpath += @"\";
                }
            }

            regkey.Close();*/
        }

        //TODO: Rewrite
        //Load the VMs from the registry
        private void LoadVMs()
        {
            /*lstVMs.Items.Clear();
            VMCountRefresh();
            try
            {
                regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box\Virtual Machines");
                VirtualMachine vm = new VirtualMachine();

                foreach (var value in regkey.GetValueNames())
                {
                    MemoryStream ms = new MemoryStream((byte[])regkey.GetValue(value));
                    BinaryFormatter bf = new BinaryFormatter();
                    vm = (VirtualMachine)bf.Deserialize(ms);
                    ms.Close();

                    ListViewItem newLvi = new ListViewItem(vm.Name)
                    {
                        Tag = vm,
                        ImageIndex = 0
                    };
                    newLvi.SubItems.Add(new ListViewItem.ListViewSubItem(newLvi, vm.GetStatusString()));
                    newLvi.SubItems.Add(new ListViewItem.ListViewSubItem(newLvi, vm.Desc));
                    newLvi.SubItems.Add(new ListViewItem.ListViewSubItem(newLvi, vm.Path));
                    lstVMs.Items.Add(newLvi);
                }

                lstVMs.SelectedItems.Clear();
                btnStartStop.Enabled = false;
                btnPauseResume.Enabled = false;
                btnEdit.Enabled = false;
                btnDelete.Enabled = false;
                btnConfigure.Enabled = false;
                btnCtrlAltDel.Enabled = false;
                btnReset.Enabled = false;

                VMCountRefresh();
            }
            catch (Exception ex)
            {
                //Ignore for now
            }*/
        }

        //Enable/disable relevant menu items depending on selected VM's status
        private void cmsVM_Opening(object sender, CancelEventArgs e)
        {
            //Available menu option differs based on the number of selected VMs
            if (lstVMs.SelectedItems.Count == 0)
            {
                e.Cancel = true;
            }
            else if (lstVMs.SelectedItems.Count == 1)
            {
                VirtualMachine vm = Get((Guid)lstVMs.SelectedItems[0].Tag);
                switch (vm.State)
                {
                    case VMState.Running:
                        {
                            startStopToolStripMenuItem.Text = "Stop";
                            startStopToolStripMenuItem.Enabled = true;
                            startStopToolStripMenuItem.ToolTipText = "Stop this virtual machine";
                            editToolStripMenuItem.Enabled = false;
                            deleteToolStripMenuItem.Enabled = false;
                            hardResetToolStripMenuItem.Enabled = true;
                            CtrlAltDelToolStripMenuItem.Enabled = true;
                            pauseResumeToolStripMenuItem.Enabled = true;
                            pauseResumeToolStripMenuItem.Text = "Pause";
                            configureToolStripMenuItem.Enabled = true;
                        }
                        break;
                    case VMState.Stopped:
                        {
                            startStopToolStripMenuItem.Text = "Start";
                            startStopToolStripMenuItem.Enabled = true;
                            startStopToolStripMenuItem.ToolTipText = "Start this virtual machine";
                            editToolStripMenuItem.Enabled = true;
                            deleteToolStripMenuItem.Enabled = true;
                            hardResetToolStripMenuItem.Enabled = false;
                            CtrlAltDelToolStripMenuItem.Enabled = false;
                            pauseResumeToolStripMenuItem.Enabled = false;
                            pauseResumeToolStripMenuItem.Text = "Pause";
                            configureToolStripMenuItem.Enabled = true;
                        }
                        break;
                    case VMState.Waiting:
                        {
                            startStopToolStripMenuItem.Enabled = false;
                            startStopToolStripMenuItem.Text = "Stop";
                            startStopToolStripMenuItem.ToolTipText = "Stop this virtual machine";
                            editToolStripMenuItem.Enabled = false;
                            deleteToolStripMenuItem.Enabled = false;
                            hardResetToolStripMenuItem.Enabled = false;
                            CtrlAltDelToolStripMenuItem.Enabled = false;
                            pauseResumeToolStripMenuItem.Enabled = false;
                            pauseResumeToolStripMenuItem.Text = "Pause";
                            pauseResumeToolStripMenuItem.ToolTipText = "Pause this virtual machine";
                            configureToolStripMenuItem.Enabled = false;
                        }
                        break;
                    case VMState.Paused:
                        {
                            startStopToolStripMenuItem.Enabled = true;
                            startStopToolStripMenuItem.Text = "Stop";
                            startStopToolStripMenuItem.ToolTipText = "Stop this virtual machine";
                            editToolStripMenuItem.Enabled = false;
                            deleteToolStripMenuItem.Enabled = false;
                            hardResetToolStripMenuItem.Enabled = true;
                            CtrlAltDelToolStripMenuItem.Enabled = true;
                            pauseResumeToolStripMenuItem.Enabled = true;
                            pauseResumeToolStripMenuItem.Text = "Resume";
                            pauseResumeToolStripMenuItem.ToolTipText = "Resume this virtual machine";
                            configureToolStripMenuItem.Enabled = true;
                        }
                        break;
                };
            }
            //Multiple VMs selected => disable most options
            else
            {
                startStopToolStripMenuItem.Text = "Start";
                startStopToolStripMenuItem.Enabled = false;
                startStopToolStripMenuItem.ToolTipText = "Start this virtual machine";
                editToolStripMenuItem.Enabled = false;
                deleteToolStripMenuItem.Enabled = true;
                hardResetToolStripMenuItem.Enabled = false;
                CtrlAltDelToolStripMenuItem.Enabled = false;
                pauseResumeToolStripMenuItem.Enabled = false;
                pauseResumeToolStripMenuItem.Text = "Pause";
                killToolStripMenuItem.Enabled = true;
                configureToolStripMenuItem.Enabled = false;
                cloneToolStripMenuItem.Enabled = false;
            }
        }

        //Closing 86Box Manager before closing all the VMs can lead to weirdness if 86Box Manager is then restarted. So let's warn the user just in case and request confirmation.
        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            int vmCount = 0; //Number of running VMs

            //Close to tray
            if (e.CloseReason == CloseReason.UserClosing && closeTray)
            {
                e.Cancel = true;
                trayIcon.Visible = true;
                WindowState = FormWindowState.Minimized;
                Hide();
            }
            else
            {
                foreach (ListViewItem lvi in lstVMs.Items)
                {
                    VirtualMachine vm = Get((Guid)lvi.Tag);
                    if (vm.State != VMState.Stopped && Visible)
                    {
                        vmCount++;
                    }
                }
            }

            //If there are running VMs, display the warning and stop the VMs if user says so
            if (vmCount > 0)
            {
                e.Cancel = true;
                DialogResult = MessageBox.Show($"{vmCount} virtual machines are still running. It's recommended you stop them first before closing 86Box Manager. Do you want to stop them now?", "Virtual machines are still running", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (DialogResult == DialogResult.Yes)
                {
                    ForceStopAll();
                }
                else if (DialogResult == DialogResult.Cancel)
                {
                    return;
                }

                e.Cancel = false;
            }

            //Save main window's state, size and position
            /*Settings.Default.WindowState = WindowState;
            Settings.Default.WindowSize = Size;
            Settings.Default.WindowPosition = Location;

            //Save listview column widths
            Settings.Default.NameColWidth = clmName.Width;
            Settings.Default.StatusColWidth = clmStatus.Width;
            Settings.Default.DescColWidth = clmDesc.Width;
            Settings.Default.PathColWidth = clmPath.Width;

            Settings.Default.Save();*/
        }

        private void pauseResumeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            VirtualMachine vm = Get((Guid)lstVMs.SelectedItems[0].Tag);
            if (vm.State == VMState.Paused)
                vm.Resume();
            else if (vm.State == VMState.Running)
                vm.Pause();

            VMCountRefresh();
        }

        //Start VM if it's stopped or stop it if it's running/paused
        private void startStopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            VirtualMachine vm = Get((Guid)lstVMs.SelectedItems[0].Tag);
            if (vm.State == VMState.Stopped)
                vm.Start();
            else if (vm.State == VMState.Running)
                vm.RequestStop();
            else if(vm.State == VMState.Paused)
            {
                vm.Resume();
                vm.RequestStop();
            }

            VMCountRefresh();
        }

        private void configureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem lvi in lstVMs.SelectedItems)
            {
                VirtualMachine vm = Get((Guid)lvi.Tag);
                if (vm.State == VMState.Running || vm.State == VMState.Paused || vm.State == VMState.Stopped)
                    vm.Configure();
            }

            VMCountRefresh();
        }

        //Opens the settings window for the selected VM
        private void VMConfigure()
        {
            /*VirtualMachine vm = (VirtualMachine)lstVMs.SelectedItems[0].Tag;

            //If the VM is already running, only send the message to open the settings window. Otherwise, start the VM with the -S parameter
            if (vm.Status == VirtualMachine.STATUS_RUNNING || vm.Status == VirtualMachine.STATUS_PAUSED)
            {
                PostMessage(vm.hWnd, 0x8889, IntPtr.Zero, IntPtr.Zero);
                SetForegroundWindow(vm.hWnd);
            }
            else if (vm.Status == VirtualMachine.STATUS_STOPPED)
            {
                try
                {
                    Process p = new Process();
                    p.StartInfo.FileName = exepath + "86Box.exe";
                    p.StartInfo.Arguments = "-S -P \"" + lstVMs.SelectedItems[0].SubItems[3].Text + "\"";
                    if (!showConsole)
                    {
                        p.StartInfo.RedirectStandardOutput = true;
                        p.StartInfo.UseShellExecute = false;
                    }
                    p.Start();
                    p.WaitForInputIdle();

                    vm.Status = VirtualMachine.STATUS_WAITING;
                    vm.hWnd = p.MainWindowHandle;
                    vm.Pid = p.Id;
                    lstVMs.SelectedItems[0].SubItems[1].Text = vm.GetStatusString();
                    lstVMs.SelectedItems[0].ImageIndex = 2;

                    BackgroundWorker bgw = new BackgroundWorker
                    {
                        WorkerReportsProgress = false,
                        WorkerSupportsCancellation = false
                    };
                    bgw.DoWork += new DoWorkEventHandler(backgroundWorker_DoWork);
                    bgw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorker_RunWorkerCompleted);
                    bgw.RunWorkerAsync(vm);

                    btnStartStop.Enabled = false;
                    btnStartStop.Text = "Stop";
                    toolTip.SetToolTip(btnStartStop, "Stop this virtual machine");
                    startToolStripMenuItem.Text = "Stop";
                    startToolStripMenuItem.ToolTipText = "Stop this virtual machine";
                    btnEdit.Enabled = false;
                    btnDelete.Enabled = false;
                    btnConfigure.Enabled = false;
                    btnReset.Enabled = false;
                    btnPauseResume.Enabled = false;
                    btnPauseResume.Text = "Pause";
                    toolTip.SetToolTip(btnPauseResume, "Pause this virtual machine");
                    pauseToolStripMenuItem.Text = "Pause";
                    pauseToolStripMenuItem.ToolTipText = "Pause this virtual machine";
                    btnCtrlAltDel.Enabled = false;
                }
                catch (Win32Exception ex)
                {
                    MessageBox.Show("Cannot find 86Box.exe. Make sure your settings are correct and try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    //Revert to stopped status and alert the user
                    vm.Status = VirtualMachine.STATUS_STOPPED;
                    vm.hWnd = IntPtr.Zero;
                    vm.Pid = -1;
                    MessageBox.Show("This virtual machine could not be started. Please provide the following information to the developer:\n" + ex.Message + "\n" + ex.StackTrace, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            VMSort(sortColumn, sortOrder);
            VMCountRefresh();*/
        }

        private void CtrlAltDelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach(ListViewItem lvi in lstVMs.SelectedItems)
            {
                VirtualMachine vm = Get((Guid)lvi.Tag);

                if (vm.State == VMState.Running)
                    vm.SendCtrAltDel();
                else if(vm.State == VMState.Paused)
                {
                    vm.Resume();
                    vm.SendCtrAltDel();
                }
            }

            VMCountRefresh();
        }

        private void hardResetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem lvi in lstVMs.SelectedItems)
            {
                VirtualMachine vm = Get((Guid)lvi.Tag);

                if (vm.State == VMState.Running)
                    vm.HardReset();
                else if (vm.State == VMState.Paused)
                {
                    vm.Resume();
                    vm.SendCtrAltDel();
                }
            }

            VMCountRefresh();
        }

        //For double clicking an item, do something based on VM status
        private void lstVMs_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            /*if (e.Button == MouseButtons.Left)
            {
                if (lstVMs.SelectedItems[0].Bounds.Contains(e.Location))
                {
                    VirtualMachine vm = (VirtualMachine)lstVMs.SelectedItems[0].Tag;
                    if (vm.Status == VirtualMachine.STATUS_STOPPED)
                    {
                        VMStart();
                    }
                    else if (vm.Status == VirtualMachine.STATUS_RUNNING)
                    {
                        VMRequestStop();
                    }
                    else if (vm.Status == VirtualMachine.STATUS_PAUSED)
                    {
                        VMResume();
                    }
                }
            }*/
        }

        //Creates a new VM from the data recieved and adds it to the listview
        public void VMAdd(string name, string desc, bool openCFG, bool startVM)
        {
            /*VirtualMachine newVM = new VirtualMachine(name, desc, cfgpath + name);
            ListViewItem newLvi = new ListViewItem(newVM.Name)
            {
                Tag = newVM,
                ImageIndex = 0
            };
            newLvi.SubItems.Add(new ListViewItem.ListViewSubItem(newLvi, newVM.GetStatusString()));
            newLvi.SubItems.Add(new ListViewItem.ListViewSubItem(newLvi, newVM.Desc));
            newLvi.SubItems.Add(new ListViewItem.ListViewSubItem(newLvi, newVM.Path));
            lstVMs.Items.Add(newLvi);
            Directory.CreateDirectory(cfgpath + newVM.Name);

            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, newVM);
                var data = ms.ToArray();
                regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box\Virtual Machines", true);
                regkey.SetValue(newVM.Name, data, RegistryValueKind.Binary);
            }

            MessageBox.Show("Virtual machine \"" + newVM.Name + "\" was successfully created!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

            //Select the newly created VM
            foreach (ListViewItem lvi in lstVMs.SelectedItems)
            {
                lvi.Selected = false;
            }
            newLvi.Focused = true;
            newLvi.Selected = true;

            //Start the VM and/or open settings window if the user chose this option
            if (startVM)
            {
                VMStart();
            }
            if (openCFG)
            {
                VMConfigure();
            }

            VMSort(sortColumn, sortOrder);
            VMCountRefresh();*/
        }

        //Checks if a VM with this name already exists
        public bool VMCheckIfExists(string name)
        {
            /*regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box\Virtual Machines", true);
            if (regkey == null) //Regkey doesn't exist yet
            {
                regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box", true);
                regkey.CreateSubKey(@"Virtual Machines");
                return false;
            }

            //VM's registry value doesn't exist yet
            if (regkey.GetValue(name) == null)
            {
                regkey.Close();
                return false;
            }
            else
            {
                regkey.Close();
                return true;
            }*/

            return false;
        }

        //Changes a VM's name and/or description
        public void VMEdit(string name, string desc)
        {
            /*VirtualMachine vm = (VirtualMachine)lstVMs.SelectedItems[0].Tag;
            string oldname = vm.Name;
            if (!vm.Name.Equals(name))
            {
                try
                { //Move the actual VM files too. This will invalidate any paths inside the cfg, but the user is informed to update those manually.
                    Directory.Move(cfgpath + vm.Name, cfgpath + name);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("An error has occurred while trying to move the files for this virtual machine. Please try to move them manually.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                vm.Name = name;
                vm.Path = cfgpath + vm.Name;
            }
            vm.Desc = desc;

            //Create a new registry value with new info, delete the old one
            regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box\Virtual Machines", true);
            using (var ms = new MemoryStream())
            {
                regkey.DeleteValue(oldname);
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, vm);
                var data = ms.ToArray();
                regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box\Virtual Machines", true);
                regkey.SetValue(vm.Name, data, RegistryValueKind.Binary);
            }
            regkey.Close();

            MessageBox.Show("Virtual machine \"" + vm.Name + "\" was successfully modified. Please update its configuration so that any absolute paths (e.g. for hard disk images) point to the new folder.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            VMSort(sortColumn, sortOrder);
            LoadVMs();*/
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            foreach(ListViewItem lvi in lstVMs.SelectedItems)
            {
                VirtualMachine vm = Get((Guid)lvi.Tag);
                if(vm.State == VMState.Stopped)
                {
                    Remove(vm.Guid);
                }
            }

            VMCountRefresh();
        }

        //Removes the selected VM. Confirmations for maximum safety
        private void VMRemove()
        {
            /*foreach (ListViewItem lvi in lstVMs.SelectedItems)
            {
                VirtualMachine vm = (VirtualMachine)lvi.Tag;//(VM)lstVMs.SelectedItems[0].Tag;
                DialogResult result1 = MessageBox.Show("Are you sure you want to remove the virtual machine \"" + vm.Name + "\"?", "Remove virtual machine", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result1 == DialogResult.Yes)
                {
                    if (vm.Status != VirtualMachine.STATUS_STOPPED)
                    {
                        MessageBox.Show("Virtual machine \"" + vm.Name + "\" is currently running and cannot be removed. Please stop virtual machines before attempting to remove them.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        continue;
                    }
                    try
                    {
                        lstVMs.Items.Remove(lvi);
                        regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box\Virtual Machines", true);
                        regkey.DeleteValue(vm.Name);
                        regkey.Close();
                    }
                    catch (Exception ex) //Catches "regkey doesn't exist" exceptions and such
                    {
                        MessageBox.Show("Virtual machine \"" + vm.Name + "\" could not be removed due to the following error:\n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        continue;
                    }

                    DialogResult result2 = MessageBox.Show("Virtual machine \"" + vm.Name + "\" was successfully removed. Would you like to delete its files as well?", "Virtual machine removed", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result2 == DialogResult.Yes)
                    {
                        try
                        {
                            Directory.Delete(vm.Path, true);
                        }
                        catch (UnauthorizedAccessException) //Files are read-only or protected by privileges
                        {
                            MessageBox.Show("86Box Manager was unable to delete the files of this virtual machine because they are read-only or you don't have sufficient privileges to delete them.\n\nMake sure the files are free for deletion, then remove them manually.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            continue;
                        }
                        catch (DirectoryNotFoundException) //Directory not found
                        {
                            MessageBox.Show("86Box Manager was unable to delete the files of this virtual machine because they no longer exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            continue;
                        }
                        catch (IOException) //Files are in use by another process
                        {
                            MessageBox.Show("86Box Manager was unable to delete some files of this virtual machine because they are currently in use by another process.\n\nMake sure the files are free for deletion, then remove them manually.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            continue;
                        }
                        catch (Exception ex)
                        { //Other exceptions
                            MessageBox.Show("The following error occurred while trying to remove the files of this virtual machine:\n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            continue;
                        }
                        MessageBox.Show("Files of virtual machine \"" + vm.Name + "\" were successfully deleted.", "Virtual machine files removed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            VMSort(sortColumn, sortOrder);
            VMCountRefresh();*/
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem lvi in lstVMs.SelectedItems)
            {
                VirtualMachine vm = Get((Guid)lvi.Tag);
                if (vm.State == VMState.Stopped)
                {
                    Remove(vm.Guid);
                }
            }

            VMCountRefresh();
        }

        private void editToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dlgEditVM dlg = new dlgEditVM();
            dlg.ShowDialog();
            dlg.Dispose();
        }

        private void btnCtrlAltDel_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem lvi in lstVMs.SelectedItems)
            {
                VirtualMachine vm = Get((Guid)lvi.Tag);

                if (vm.State == VMState.Running)
                    vm.SendCtrAltDel();
                else if (vm.State == VMState.Paused)
                {
                    vm.Resume();
                    vm.SendCtrAltDel();
                }
            }

            VMCountRefresh();
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem lvi in lstVMs.SelectedItems)
            {
                VirtualMachine vm = Get((Guid)lvi.Tag);

                if (vm.State == VMState.Running)
                    vm.HardReset();
                else if (vm.State == VMState.Paused)
                {
                    vm.Resume();
                    vm.SendCtrAltDel();
                }
            }

            VMCountRefresh();
        }

        private void btnPauseResume_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem lvi in lstVMs.SelectedItems)
            {
                VirtualMachine vm = Get((Guid)lvi.Tag);
                if (vm.State == VMState.Running)
                {
                    vm.Pause();
                }
            }

            VMCountRefresh();
        }

        //This function monitors received window messages
        protected override void WndProc(ref Message m)
        {
            // 0x8895 - VM paused/resumed, wparam = 1: VM paused, wparam = 0: VM resumed
            // 0x8896 - Dialog opened/closed, wparam = 1: opened, wparam = 0: closed
            // 0x8897 - Shutdown confirmed
            if (m.Msg == 0x8895)
            {
                if (m.WParam.ToInt32() == 1) //VM was paused
                {
                    VirtualMachine vm = Get(m.LParam);
                    vm.State = VMState.Paused;

                    pauseResumeToolStripMenuItem.Text = "Resume";
                    btnPauseResume.Text = "Resume";
                    pauseResumeToolStripMenuItem.ToolTipText = "Resume this virtual machine";
                    toolTip.SetToolTip(btnPauseResume, "Resume this virtual machine");
                    btnStartStop.Enabled = true;
                    btnStartStop.Text = "Stop";
                    startStopToolStripMenuItem.Text = "Stop";
                    startStopToolStripMenuItem.ToolTipText = "Stop this virtual machine";
                    toolTip.SetToolTip(btnStartStop, "Stop this virtual machine");
                    btnConfigure.Enabled = true;

                    /*lvi.SubItems[1].Text = "Paused";
                    lvi.ImageIndex = 2;*/
                    //UpdateListViewItem(vm.Guid);

                    VMCountRefresh();
                }
                else if (m.WParam.ToInt32() == 0) //VM was resumed
                {
                    VirtualMachine vm = Get(m.LParam);
                    vm.State = VMState.Running;

                    pauseResumeToolStripMenuItem.Text = "Pause";
                    btnPauseResume.Text = "Pause";
                    toolTip.SetToolTip(btnPauseResume, "Pause this virtual machine");
                    pauseResumeToolStripMenuItem.ToolTipText = "Pause this virtual machine";
                    btnStartStop.Enabled = true;
                    btnStartStop.Text = "Stop";
                    toolTip.SetToolTip(btnStartStop, "Stop this virtual machine");
                    startStopToolStripMenuItem.Text = "Stop";
                    startStopToolStripMenuItem.ToolTipText = "Stop this virtual machine";
                    btnConfigure.Enabled = true;

                    /*lvi.SubItems[1].Text = "Running";
                    lvi.ImageIndex = 1;*/
                    //UpdateListViewItem(vm.Guid);

                    VMCountRefresh();
                }
            }
            if (m.Msg == 0x8896)
            {
                if (m.WParam.ToInt32() == 1)  //A dialog was opened
                {
                    VirtualMachine vm = Get(m.LParam);
                    vm.State = VMState.Waiting;

                    btnStartStop.Enabled = false;
                    btnStartStop.Text = "Stop";
                    toolTip.SetToolTip(btnStartStop, "Stop this virtual machine");
                    startStopToolStripMenuItem.Text = "Stop";
                    startStopToolStripMenuItem.ToolTipText = "Stop this virtual machine";
                    btnEdit.Enabled = false;
                    btnDelete.Enabled = false;
                    btnConfigure.Enabled = false;
                    btnReset.Enabled = false;
                    btnPauseResume.Enabled = false;
                    btnCtrlAltDel.Enabled = false;

                    /*lvi.SubItems[1].Text = "Waiting";
                    lvi.ImageIndex = 2;*/
                    //UpdateListViewItem(vm.Guid);

                    VMCountRefresh();
                }
                else if (m.WParam.ToInt32() == 0) //A dialog was closed
                {
                    VirtualMachine vm = Get(m.LParam);
                    vm.State = VMState.Running;

                    btnStartStop.Enabled = true;
                    btnStartStop.Text = "Stop";
                    toolTip.SetToolTip(btnStartStop, "Stop this virtual machine");
                    startStopToolStripMenuItem.Text = "Stop";
                    startStopToolStripMenuItem.ToolTipText = "Stop this virtual machine";
                    btnEdit.Enabled = false;
                    btnDelete.Enabled = false;
                    btnConfigure.Enabled = true;
                    btnReset.Enabled = true;
                    btnPauseResume.Enabled = true;
                    btnPauseResume.Text = "Pause";
                    pauseResumeToolStripMenuItem.Text = "Pause";
                    pauseResumeToolStripMenuItem.ToolTipText = "Pause this virtual machine";
                    toolTip.SetToolTip(btnPauseResume, "Pause this virtual machine");
                    btnCtrlAltDel.Enabled = true;

                    /*lvi.SubItems[1].Text = "Running";
                    lvi.ImageIndex = 1;*/
                    //UpdateListViewItem(vm.Guid);

                    VMCountRefresh();
                }
            }
            if (m.Msg == 0x8897) //Shutdown confirmed
            {
                VirtualMachine vm = Get(m.LParam);
                vm.State = VMState.Stopped;
                vm.Handle = IntPtr.Zero;
                vm.ProcessID = -1;

                /*lvi.SubItems[1].Text = vm.GetStatusString();
                lvi.ImageIndex = 0;*/
                //UpdateListViewItem(vm.Guid);

                btnStartStop.Text = "Start";
                startStopToolStripMenuItem.Text = "Start";
                startStopToolStripMenuItem.ToolTipText = "Start this virtual machine";
                toolTip.SetToolTip(btnStartStop, "Start this virtual machine");
                btnPauseResume.Text = "Pause";
                pauseResumeToolStripMenuItem.ToolTipText = "Pause this virtual machine";
                pauseResumeToolStripMenuItem.Text = "Pause";
                toolTip.SetToolTip(btnPauseResume, "Pause this virtual machine");
                if (lstVMs.SelectedItems.Count == 1)
                {
                    btnEdit.Enabled = true;
                    btnDelete.Enabled = true;
                    btnStartStop.Enabled = true;
                    btnConfigure.Enabled = true;
                    btnPauseResume.Enabled = false;
                    btnReset.Enabled = false;
                    btnCtrlAltDel.Enabled = false;
                }
                else if (lstVMs.SelectedItems.Count == 0)
                {
                    btnEdit.Enabled = false;
                    btnDelete.Enabled = false;
                    btnStartStop.Enabled = false;
                    btnConfigure.Enabled = false;
                    btnPauseResume.Enabled = false;
                    btnReset.Enabled = false;
                    btnCtrlAltDel.Enabled = false;
                }
                else
                {
                    btnEdit.Enabled = false;
                    btnDelete.Enabled = true;
                    btnStartStop.Enabled = false;
                    btnConfigure.Enabled = false;
                    btnPauseResume.Enabled = false;
                    btnReset.Enabled = false;
                    btnCtrlAltDel.Enabled = false;
                }

                VMCountRefresh();
            }
            //This is the WM_COPYDATA message, used here to pass command line args to an already running instance
            //NOTE: This code will have to be modified in case more command line arguments are added in the future.
            if (m.Msg == 0x004A)
            {
                //Get the VM name and find the associated LVI and VM object
                COPYDATASTRUCT ds = (COPYDATASTRUCT)m.GetLParam(typeof(COPYDATASTRUCT));
                string vmName = Marshal.PtrToStringAnsi(ds.lpData, ds.cbData);
                ListViewItem lvi = lstVMs.FindItemWithText(vmName);

                //This check is necessary in case the specified VM was already removed but the shortcut remains
                if (lvi != null)
                {
                    VirtualMachine vm = (VirtualMachine)lvi.Tag;

                    //If the VM is already running, display a message, otherwise, start it
                    if (vm.State != VMState.Stopped)
                        MessageBox.Show("The virtual machine \"" + vmName + "\" is already running.", "Virtual machine already running", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else
                    {
                        //This is needed so that we start the correct VM in case multiple items are selected
                        /*lstVMs.SelectedItems.Clear();

                        lvi.Focused = true;
                        lvi.Selected = true;
                        VMStart();*/
                    }
                }
                else
                    MessageBox.Show("The virtual machine \"" + vmName + "\" could not be found. It may have been removed or the specified name is incorrect.", "Virtual machine not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            base.WndProc(ref m);
        }

        private void openFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem lvi in lstVMs.SelectedItems)
            {
                VirtualMachine vm = Get((Guid)lvi.Tag);
                vm.OpenFolder();
            }
        }

        private void createADesktopShortcutToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        //Starts/stops selected VMs when Enter is pressed, or deletes them if Delete is pressed
        private void lstVMs_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                foreach (ListViewItem lvi in lstVMs.SelectedItems)
                {
                    VirtualMachine vm = Get((Guid)lvi.Tag);
                    if (vm.State == VMState.Running)
                        vm.RequestStop();
                    else if (vm.State == VMState.Stopped)
                        vm.Start();
                    else if(vm.State == VMState.Paused)
                    {
                        vm.Resume();
                        vm.RequestStop();
                    }
                }
            }
            if (e.KeyCode == Keys.Delete)
            {
                foreach (ListViewItem lvi in lstVMs.SelectedItems)
                    Remove((Guid)lvi.Tag);
            }

            VMCountRefresh();
        }

        private void trayIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            //Restore the window and hide the tray icon
            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
            trayIcon.Visible = false;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int vmCount = 0;
            foreach (ListViewItem lvi in lstVMs.Items)
            {
                VirtualMachine vm = Get((Guid)lvi.Tag);
                if (vm.State != VMState.Stopped)
                    vmCount++;
            }

            //If there are running VMs, display the warning and stop the VMs if user says so
            if (vmCount > 0)
            {
                DialogResult = MessageBox.Show($"{vmCount} virtual machines are still running. It's recommended you stop them first before closing 86Box Manager. Do you want to stop them now?", "Virtual machines are still running", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (DialogResult == DialogResult.Yes)
                {
                    ForceStopAll();

                    Thread.Sleep(vmCount * 500); //Wait just a bit to make sure everything goes as planned
                }
                else if (DialogResult == DialogResult.Cancel)
                {
                    return;
                }
            }

            Application.Exit();
        }

        //Handles things when WindowState changes
        private void frmMain_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized && minimizeTray)
            {
                trayIcon.Visible = true;
                Hide();
            }
            if (WindowState == FormWindowState.Normal)
            {
                Show();
                trayIcon.Visible = false;
            }
        }

        private void open86BoxManagerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
            trayIcon.Visible = false;
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
            trayIcon.Visible = false;
            dlgSettings ds = new dlgSettings();
            ds.ShowDialog();
            LoadSettings();
            ds.Dispose();
        }

        //Kills the process associated with the selected VM
        private void VMKill()
        {
            /*foreach (ListViewItem lvi in lstVMs.SelectedItems)
            {
                VirtualMachine vm = (VirtualMachine)lvi.Tag;

                //Ask the user to confirm
                DialogResult = MessageBox.Show("Killing a virtual machine can cause data loss. Only do this if 86Box.exe process gets stuck.\n\nDo you really wish to kill the virtual machine \"" + vm.Name + "\"?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (DialogResult == DialogResult.Yes)
                {
                    try
                    {
                        Process p = Process.GetProcessById(vm.Pid);
                        p.Kill();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Could not kill 86Box.exe process for virtual machine \"" + vm.Name + "\". The process may have already ended on its own or access was denied.", "Could not kill process", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        continue;
                    }

                    //We need to cleanup afterwards to make sure the VM is put back into a valid state
                    vm.Status = VirtualMachine.STATUS_STOPPED;
                    vm.hWnd = IntPtr.Zero;
                    lstVMs.SelectedItems[0].SubItems[1].Text = vm.GetStatusString();
                    lstVMs.SelectedItems[0].ImageIndex = 0;

                    btnStartStop.Text = "Start";
                    toolTip.SetToolTip(btnStartStop, "Stop this virtual machine");
                    btnPauseResume.Text = "Pause";
                    if (lstVMs.SelectedItems.Count > 0)
                    {
                        btnEdit.Enabled = true;
                        btnDelete.Enabled = true;
                        btnStartStop.Enabled = true;
                        btnConfigure.Enabled = true;
                        btnPauseResume.Enabled = false;
                        btnReset.Enabled = false;
                        btnCtrlAltDel.Enabled = false;
                    }
                    else
                    {
                        btnEdit.Enabled = false;
                        btnDelete.Enabled = false;
                        btnStartStop.Enabled = false;
                        btnConfigure.Enabled = false;
                        btnPauseResume.Enabled = false;
                        btnReset.Enabled = false;
                        btnCtrlAltDel.Enabled = false;
                    }
                }
            }

            VMSort(sortColumn, sortOrder);
            VMCountRefresh();*/
        }

        private void killToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach(ListViewItem lvi in lstVMs.SelectedItems)
            {
                VirtualMachine vm = Get((Guid)lvi.Tag);
                if(vm.State != VMState.Stopped)
                    vm.Kill();
            }
        }

        //Sort the VM list by specified column and order
        private void VMSort(int column, SortOrder order)
        {
            /*const string ascArrow = " ▲";
            const string descArrow = " ▼";

            if (lstVMs.SelectedItems.Count > 1)
            {
                lstVMs.SelectedItems.Clear(); //Just in case so we don't end up with weird selection glitches
            }

            //Remove the arrows from the current column text if they exist
            if (sortColumn > -1 && (lstVMs.Columns[sortColumn].Text.EndsWith(ascArrow) || lstVMs.Columns[sortColumn].Text.EndsWith(descArrow)))
            {
                lstVMs.Columns[sortColumn].Text = lstVMs.Columns[sortColumn].Text.Substring(0, lstVMs.Columns[sortColumn].Text.Length - 2);
            }

            //Then append the appropriate arrow to the new column text
            if (order == SortOrder.Ascending)
            {
                lstVMs.Columns[column].Text += ascArrow;
            }
            else if (order == SortOrder.Descending)
            {
                lstVMs.Columns[column].Text += descArrow;
            }

            sortColumn = column;
            sortOrder = order;
            lstVMs.Sorting = sortOrder;
            lstVMs.Sort();
            lstVMs.ListViewItemSorter = new ListViewItemComparer(sortColumn, sortOrder);

            //Save the new column and order to the registry
            regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box", true);
            regkey.SetValue("SortColumn", sortColumn, RegistryValueKind.DWord);
            regkey.SetValue("SortOrder", sortOrder, RegistryValueKind.DWord);
            regkey.Close();*/
        }

        //Handles the click event for the listview column headers, allowing to sort the items by columns
        private void lstVMs_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            /*if (lstVMs.Sorting == SortOrder.Ascending)
            {
                VMSort(e.Column, SortOrder.Descending);
            }
            else if (lstVMs.Sorting == SortOrder.Descending || lstVMs.Sorting == SortOrder.None)
            {
                VMSort(e.Column, SortOrder.Ascending);
            }*/
        }

        private void wipeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach(ListViewItem lvi in lstVMs.SelectedItems)
            {
                VirtualMachine vm = Get((Guid)lvi.Tag);
                if(vm.State == VMState.Stopped)
                {
                    vm.Wipe();
                }
            }
        }

        //Deletes the config and nvr of selected VM
        private void VMWipe()
        {
            /*foreach (ListViewItem lvi in lstVMs.SelectedItems)
            {
                VirtualMachine vm = (VirtualMachine)lvi.Tag;

                DialogResult = MessageBox.Show("Wiping a virtual machine deletes its configuration and nvr files. You'll have to reconfigure the virtual machine (and the BIOS if applicable).\n\n Are you sure you wish to wipe the virtual machine \"" + vm.Name + "\"?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (DialogResult == DialogResult.Yes)
                {
                    if (vm.State != VMState.Stopped)
                    {
                        MessageBox.Show("The virtual machine \"" + vm.Name + "\" is currently running and cannot be wiped. Please stop virtual machines before attempting to wipe them.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        continue;
                    }
                    try
                    {
                        System.IO.File.Delete(vm.Path + @"\86box.cfg");
                        Directory.Delete(vm.Path + @"\nvr", true);
                        MessageBox.Show("The virtual machine \"" + vm.Name + "\" was successfully wiped.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("An error occurred trying to wipe the virtual machine \"" + vm.Name + "\".", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        continue;
                    }
                }
            }*/
        }

        //Imports existing VM files to a new VM
        public void VMImport(string name, string desc, string importPath, bool openCFG, bool startVM)
        {
            /*VirtualMachine newVM = new VirtualMachine(name, desc, cfgpath + name);
            ListViewItem newLvi = new ListViewItem(newVM.Name)
            {
                Tag = newVM,
                ImageIndex = 0
            };
            newLvi.SubItems.Add(new ListViewItem.ListViewSubItem(newLvi, newVM.GetStatusString()));
            newLvi.SubItems.Add(new ListViewItem.ListViewSubItem(newLvi, newVM.Desc));
            newLvi.SubItems.Add(new ListViewItem.ListViewSubItem(newLvi, newVM.Path));
            lstVMs.Items.Add(newLvi);
            Directory.CreateDirectory(cfgpath + newVM.Name);

            bool importFailed = false;

            //Copy existing files to the new VM directory
            try
            {
                foreach (string oldPath in Directory.GetDirectories(importPath, "*", SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(oldPath.Replace(importPath, newVM.Path));
                }
                foreach (string newPath in Directory.GetFiles(importPath, "*.*", SearchOption.AllDirectories))
                {
                    System.IO.File.Copy(newPath, newPath.Replace(importPath, newVM.Path), true);
                }
            }
            catch (Exception ex)
            {
                importFailed = true; //Set this flag so we can inform the user at the end
            }

            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, newVM);
                var data = ms.ToArray();
                regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box\Virtual Machines", true);
                regkey.SetValue(newVM.Name, data, RegistryValueKind.Binary);
            }

            if (importFailed)
            {
                MessageBox.Show("Virtual machine \"" + newVM.Name + "\" was successfully created, but files could not be imported. Make sure the path you selected was correct and valid.\n\nIf the VM is already located in your VMs folder, you don't need to select the Import option, just add a new VM with the same name.", "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show("Virtual machine \"" + newVM.Name + "\" was successfully created, files were imported. Remember to update any paths pointing to disk images in your config!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            //Select the newly created VM
            foreach(ListViewItem lvi in lstVMs.SelectedItems)
            {
                lvi.Selected = false;
            }
            newLvi.Focused = true;
            newLvi.Selected = true;

            //Start the VM and/or open settings window if the user chose this option
            if (startVM)
            {
                VMStart();
            }
            if (openCFG)
            {
                VMConfigure();
            }

            VMSort(sortColumn, sortOrder);
            VMCountRefresh();*/
        }

        private void cloneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /*VirtualMachine vm = (VirtualMachine)lstVMs.SelectedItems[0].Tag;
            dlgCloneVM dc = new dlgCloneVM(vm.Path);
            dc.ShowDialog();
            dc.Dispose();*/
        }

        //Refreshes the VM counter in the status bar
        private void VMCountRefresh()
        {
            int runningVMs = 0;
            int pausedVMs = 0;
            int waitingVMs = 0;
            int stoppedVMs = 0;

            foreach (ListViewItem lvi in lstVMs.Items)
            {
                VirtualMachine vm = (VirtualMachine)lvi.Tag;
                switch (vm.State)
                {
                    case VMState.Paused: pausedVMs++; break;
                    case VMState.Running: runningVMs++; break;
                    case VMState.Stopped: stoppedVMs++; break;
                    case VMState.Waiting: waitingVMs++; break;
                }
            }

            lblVMCount.Text = "All VMs: " + lstVMs.Items.Count + " | Running: " + runningVMs + " | Paused: " + pausedVMs + " | Waiting: " + waitingVMs + " | Stopped: " + stoppedVMs;
        }

        private void openConfigFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //VMOpenConfig();
        }

        private void VMOpenConfig()
        {
            /*foreach (ListViewItem lvi in lstVMs.SelectedItems)
            {
                VirtualMachine vm = (VirtualMachine)lvi.Tag;
                try
                {
                    Process.Start(vm.Path + Path.DirectorySeparatorChar + "86box.cfg");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("The config file for the virtual machine \"" + vm.Name + "\" could not be opened. Make sure it still exists and that you have sufficient privileges to access it.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }*/
        }
    }
}