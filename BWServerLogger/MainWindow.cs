﻿using log4net;

using MySql.Data.Common;
using MySql.Data.MySqlClient;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using BWServerLogger.DAO;
using BWServerLogger.Job;
using BWServerLogger.Model;
using BWServerLogger.Service;
using BWServerLogger.Util;

namespace BWServerLogger
{
    public partial class MainWindow : Form
    {
        private const string _TIME_VALIDATION = "^[0-9][0-9]:[0-9][0-9]:[0-9][0-9]$";
        private static readonly ILog _logger = LogManager.GetLogger(typeof(MainWindow));

        private bool _closeThreads = true;
        private Thread _reportingThread;

        public MainWindow()
        {
            StartScheduleThread();
            InitializeComponent();
        }

        private void CheckThreadStatuses()
        {
            if (IsScheduleRunning())
            {
                this.SchedulerRunning.BackColor = Color.Green;
                this.ScheduleInput.Text = "Stop Scheduler";
            }
            else
            {
                this.SchedulerRunning.BackColor = Color.Red;
                this.ScheduleInput.Text = "Start Scheduler";
            }

            if (IsReportRunning()) //do this better
            {
                this.ReportStatus.BackColor = Color.Green;
                this.ReportingInput.Text = "Stop Reporting";
            }
            else
            {
                this.ReportStatus.BackColor = Color.Red;
                this.ReportingInput.Text = "Start Reporting";
            }
        }

        private void StartScheduleThread()
        {
            try
            {
                _reportingThread = new Thread(new ReportingJob().StartJob);
                _reportingThread.Start();
            }
            catch (Exception e)
            {
                _logger.Error("Scheduler could not be constructed or started", e);
            }
        }

        private void ForceReportRun()
        {
            try
            {
                _reportingThread = new Thread(new ReportingJob().ForceJobRun);
                _reportingThread.Start();
            }
            catch (Exception e)
            {
                _logger.Error("Could not force report to run", e);
            }
        }

        private void MainWindow_Load(object sender, EventArgs e)
        {
            // set up settings
            this.MySQLServerAddressInput.Text = Properties.Settings.Default.mySQLServerAddress;
            this.MySQLServerPortInput.Value = Convert.ToDecimal(Properties.Settings.Default.mySQLServerPort);
            this.MySQLServerDatabaseInput.Text = Properties.Settings.Default.mySQLServerDatabase;
            this.MySQLServerUsernameInput.Text = Properties.Settings.Default.mySQLServerUsername;
            this.MySQLServerPasswordInput.Text = DatabaseUtil.GetMySQLPassword();

            this.ArmA3MissionThresholdInput.Value = Properties.Settings.Default.missionThreshold;
            this.ArmA3PlayedThresholdInput.Value = Properties.Settings.Default.playedThreshold;
            this.ArmA3RunTimeThresholdInput.Value = ToSeconds(Properties.Settings.Default.runTimeThreshold);
            this.ArmA3ServerAddressInput.Text = Properties.Settings.Default.armaServerAddress;
            this.ArmA3ServerPollRateInput.Value = ToSeconds(Properties.Settings.Default.pollRate);
            this.ArmA3ServerPortInput.Value = Properties.Settings.Default.armaServerPort;
            this.serverReconnectLimitInput.Value = ToSeconds(Properties.Settings.Default.retryTimeLimit);

            // set up scheduler
            this.scheduleBindingSource.Clear();

            MySqlConnection connection = null;
            try
            {
                connection = DatabaseUtil.OpenDataSource();
                
                foreach (Schedule scheduleItem in new ScheduleDAO(connection).GetScheduleItems())
                {
                    this.scheduleBindingSource.Add(scheduleItem);
                }
            }
            catch (MySqlException ex)
            {
                _logger.Error("Problem getting schedule items", ex);
            }
            finally
            {
                if (connection != null)
                {
                    connection.Close();
                }
            }
        }

        private void MainWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (_closeThreads)
            {
                StopReportingJobThread();
            }
        }

        private void ScheduleInput_Click(object sender, EventArgs e)
        {
            if (IsScheduleRunning())
            {
                StopReportingJobThread();
            }
            else
            {
                StartReportingJobThread();
            }
        }

        private int ToSeconds(int milliseconds)
        {
            return milliseconds / 1000;
        }

        private int ToMilliseconds(decimal seconds)
        {
            return (int) seconds * 1000;
        }

        private void Save_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.mySQLServerAddress = this.MySQLServerAddressInput.Text;
            Properties.Settings.Default.mySQLServerPort = Convert.ToString(this.MySQLServerPortInput.Value);
            Properties.Settings.Default.mySQLServerDatabase = this.MySQLServerDatabaseInput.Text;
            Properties.Settings.Default.mySQLServerUsername = this.MySQLServerUsernameInput.Text;
            DatabaseUtil.SetMySQLPassword(this.MySQLServerPasswordInput.Text);
            Properties.Settings.Default.missionThreshold = (int)this.ArmA3MissionThresholdInput.Value;
            Properties.Settings.Default.playedThreshold = (int)this.ArmA3PlayedThresholdInput.Value;
            Properties.Settings.Default.runTimeThreshold = ToMilliseconds(this.ArmA3RunTimeThresholdInput.Value);
            Properties.Settings.Default.armaServerAddress = this.ArmA3ServerAddressInput.Text;
            Properties.Settings.Default.pollRate = ToMilliseconds(this.ArmA3ServerPollRateInput.Value);
            Properties.Settings.Default.armaServerPort = (int)this.ArmA3ServerPortInput.Value;
            Properties.Settings.Default.retryTimeLimit = ToMilliseconds(this.serverReconnectLimitInput.Value);

            Properties.Settings.Default.Save();
            MainWindow_Load(sender, e);
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            MainWindow_Load(sender, e);
        }

        private void ScheduleGrid_CancelRowEdit(object sender, QuestionEventArgs e)
        {
            MainWindow_Load(sender, e);
        }

        private void ScheduleGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            this.scheduleBindingSource.Position = e.RowIndex;
            try
            {
                if (!IsReportRunning())
                {
                    StopReportingJobThread();
                }

                MySqlConnection connection = null;
                try
                {
                    connection = DatabaseUtil.OpenDataSource();
                    new ScheduleDAO(connection).SaveScheduleItem((Schedule)this.scheduleBindingSource.Current);
                }
                catch (MySqlException ex)
                {
                    _logger.Error("Problem saving schedule items", ex);
                }
                finally
                {
                    if (connection != null)
                    {
                        connection.Close();
                    }
                }

                if (!IsScheduleRunning())
                {
                    StartReportingJobThread();
                }
            }
            catch (MySqlException ex)
            {
                _logger.Error("Problem getting schedule items", ex);
            }
            MainWindow_Load(sender, e);
        }

        private void StartReportingJobThread()
        {
            if (_reportingThread == null)
            {
                StartScheduleThread();
            }
        }

        private void StopReportingJobThread()
        {
            if (_reportingThread == null)
            {
                _logger.Info("Cannot stop reporting thread, alreaded halted");
            }
            else
            {
                try
                {

                    _reportingThread.Abort();
                    _reportingThread = null;
                }
                catch (Exception e)
                {
                    _logger.Error("Scheduler could not be stopped", e);
                }
            }
        }

        private bool IsReportRunning()
        {
            return _reportingThread != null && _reportingThread.ThreadState == ThreadState.Running;
        }

        private bool IsScheduleRunning()
        {
            return _reportingThread != null;
        }

        private void ScheduleGrid_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            try
            {
                if (!IsReportRunning())
                {
                    StopReportingJobThread();
                }

                MySqlConnection connection = null;
                try
                {
                    connection = DatabaseUtil.OpenDataSource();
                    new ScheduleDAO(connection).RemoveScheduleItem((Schedule)e.Row.DataBoundItem);
                }
                catch (MySqlException ex)
                {
                    _logger.Error("Problem removing schedule items", ex);
                }
                finally
                {
                    if (connection != null)
                    {
                        connection.Close();
                    }
                }

                if (!IsScheduleRunning())
                {
                    StartReportingJobThread();
                }
            }
            catch (MySqlException ex)
            {
                _logger.Error("Problem getting schedule items", ex);
            }
        }

        private void CloseInput_Click(object sender, EventArgs e)
        {
            _closeThreads = false;
            base.Close();
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            CheckThreadStatuses();
        }

        private void ReportingInput_Click(object sender, EventArgs e)
        {
            if (_reportingThread == null) // Job has been halted
            {
                _reportingThread = new Thread(new ReportingJob().ForceJobRun);
                _reportingThread.Start();
            }
            else
            {
                if (_reportingThread.ThreadState == ThreadState.Running) // Report running
                {
                    StopReportingJobThread();
                }
                else // waiting for next report run
                {
                    ForceReportRun();
                }
            }
        }

        private void ScheduleGrid_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            ScheduleGrid.Rows[e.RowIndex].ErrorText = "";

            if (ScheduleGrid.Rows[e.RowIndex].IsNewRow)
            {
                return;
            }

            if (e.ColumnIndex == 2)
            {
                Regex validation = new Regex(_TIME_VALIDATION);
                if (!validation.IsMatch((string)e.FormattedValue))
                {
                    e.Cancel = true;
                    ScheduleGrid.Rows[e.RowIndex].ErrorText = "The time of day value must be in 'HH:mm:ss' format";
                }
            }
        }
    }
}
