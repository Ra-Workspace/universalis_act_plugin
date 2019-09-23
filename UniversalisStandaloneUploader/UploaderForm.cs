﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Machina;
using Machina.FFXIV;
using Nhaama.FFXIV;
using UniversalisCommon;
using UniversalisStandaloneUploader.Properties;
using Definitions = UniversalisPlugin.Definitions;

namespace UniversalisStandaloneUploader
{
    public partial class UploaderForm : Form
    {
        private FFXIVNetworkMonitor _ffxivNetworkMonitor;
        private PacketProcessor _packetProcessor;

        private const string ApiKey = "xQAqN1PTellr4hZQfbgeIwp4zDCutFFUferOHBuN";

        public UploaderForm()
        {
            InitializeComponent();

            winPCapCheckBox.Checked = Properties.Settings.Default.UseWinPCap;

            try
            {
                if (Settings.Default.UpgradeRequired)
                {
                    Settings.Default.Upgrade();
                    Settings.Default.UpgradeRequired = false;
                    Settings.Default.Save();
                }
            }catch(Exception ex)
            {
                Log("Settings upgrade failed: " + ex);
            }

            if (Properties.Settings.Default.FirstLaunch)
            {
                Properties.Settings.Default.FirstLaunch = false;
                Properties.Settings.Default.Save();

                MessageBox.Show(
                    "Thank you for using the Universalis uploader!\n\nPlease don't forget to whitelist the uploader in your windows firewall, like you would with ACT.\nIt will not be able to process market board data otherwise.\nTo start uploading, log in with your character.", "Universalis Uploader", MessageBoxButtons.OK);
            }

            #if DEBUG
            Log(Definitions.GetJson());
#endif
        }

        private void UploaderForm_Resize(object sender, EventArgs e)
        {
            //if the form is minimized  
            //hide it from the task bar  
            //and show the system tray icon (represented by the NotifyIcon control)  
            if (this.WindowState == FormWindowState.Minimized)  
            {  
                Hide();  
                ShowTrayIcon();
            }  
        }

        public void ShowTrayIcon()
        {
            systemTrayIcon.Visible = true;     
            systemTrayIcon.ShowBalloonTip(1000);
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void ShowLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Show();
        }

        private void SystemTrayIcon_DoubleClick(object sender, EventArgs e)
        {
            Show();
        }
        public void Log(string text)
        {
            logTextBox.AppendText($"{text}\n");
        }

        private void UploaderForm_Load(object sender, EventArgs e)
        {
            try
            {
                _packetProcessor = new PacketProcessor(ApiKey);
                _packetProcessor.Log += (o, message) => 
                    this.BeginInvoke(new Action(() => Log(message)));

                _packetProcessor.LocalContentIdUpdated += (o, cid) =>
                    this.BeginInvoke(new Action(() =>
                    {
                        Properties.Settings.Default.LastContentId = cid;
                        Properties.Settings.Default.Save();
                    }));

                _packetProcessor.LocalContentId = Properties.Settings.Default.LastContentId;
                _packetProcessor.RequestContentIdUpdate = RequestContentIdUpdate;

                InitializeNetworkMonitor();

                Log("Uploader initialized.");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Could not initialize:\n{ex}");
            }
        }

        private void RequestContentIdUpdate(object sender, EventArgs e)
        {
            try
            {
                var process = _ffxivNetworkMonitor.ProcessID == 0 ? Process.GetProcessesByName("ffxiv_dx11")[0] : Process.GetProcessById((int) _ffxivNetworkMonitor.ProcessID);

                var game = new Game(process);
                game.Update();

                _packetProcessor.LocalContentId = game.LocalContentId;
            }
            catch (Exception ex)
            {
                this.BeginInvoke(new Action(() => Log($"[ERROR] Could not access game memory:\n{ex}")));
            }
        }

        private void InitializeNetworkMonitor()
        {
            _ffxivNetworkMonitor?.Stop();

            _ffxivNetworkMonitor = new FFXIVNetworkMonitor();
            _ffxivNetworkMonitor.MessageReceived += (connection, epoch, message) =>
                _packetProcessor?.ProcessZonePacket(message);

            _ffxivNetworkMonitor.MonitorType = TCPNetworkMonitor.NetworkMonitorType.RawSocket;

            if (winPCapCheckBox.Checked)
                _ffxivNetworkMonitor.MonitorType = TCPNetworkMonitor.NetworkMonitorType.WinPCap;

            _ffxivNetworkMonitor.Start();
        }

        private void UploaderForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.WindowsShutDown || MessageBox.Show("Do you want to stop uploading market board data?", "Universalis Uploader",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
            {
                try
                {
                    _ffxivNetworkMonitor.Stop();
                }
                finally
                {
                    Environment.Exit(0);
                }
            }
            else
            {
                e.Cancel = true;
                Hide();  
                ShowTrayIcon();
            }
        }

        private void WinPCapCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.UseWinPCap = winPCapCheckBox.Checked;
            Properties.Settings.Default.Save();

            try
            {
                InitializeNetworkMonitor();
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Could not re-initialize network monitor:\n{ex}");
            }
        }
    }
}
