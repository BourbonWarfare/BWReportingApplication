﻿using log4net;

using MySql.Data.MySqlClient;

using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

using BWServerLogger.DAO;
using BWServerLogger.Job;
using BWServerLogger.Model;
using BWServerLogger.Util;

namespace BWServerLogger {
    /// <summary>
    /// Main window class
    /// </summary>
    public partial class MainWindow : Form {
        private const string _TIME_VALIDATION = "^[0-9][0-9]:[0-9][0-9]:[0-9][0-9]$";
        private static readonly ILog _logger = LogManager.GetLogger(typeof(MainWindow));

        private bool _closeThreads = true;
        private readonly ReportingJob _reportingJob = new ReportingJob();
        private Thread _reportingThread;
        
        /// <summary>
        /// Constructor for the main window of the application
        /// </summary>
        public MainWindow() {
            _reportingThread = new Thread(_reportingJob.DoJob);
            StartScheduleThread(false);
            InitializeComponent();
        }

        private void CheckThreadStatuses() {
            if (IsScheduleRunning()) {
                SchedulerRunning.BackColor = Color.Green;
                ScheduleInput.Text = "Stop Scheduler";
            } else {
                SchedulerRunning.BackColor = Color.Red;
                ScheduleInput.Text = "Start Scheduler";
            }

            if (IsReportRunning()) {
                ReportStatus.BackColor = Color.Green;
                ReportingInput.Text = "Stop Reporting";
            } else {
                ReportStatus.BackColor = Color.Red;
                ReportingInput.Text = "Start Reporting";
            }
        }

        private void StartScheduleThread(bool forced) {
            if (!_reportingThread.IsAlive || forced) {
                try {
                    _reportingThread = new Thread(_reportingJob.DoJob);
                    _reportingThread.Start(forced);
                } catch (Exception e) {
                    _logger.Error("Scheduler could not be constructed or started", e);
                }
            } else {
                _logger.Warn("Attempting to start schedule thread while already running");
            }
        }

        private void StopScheduleThread() {
            if (!_reportingThread.IsAlive) {
                _logger.Info("Cannot stop reporting thread, alreaded halted");
            } else {
                try {
                    _reportingThread.Abort();
                    _reportingThread.Join();
                } catch (Exception e) {
                    _logger.Error("Reporting could not be stopped", e);
                }
            }
        }

        private bool PreSaveStepAndState() {
            bool scheduleState = IsScheduleRunning();
            if (!IsReportRunning()) {
                StopScheduleThread();
            }

            return scheduleState;
        }

        private void PostSaveStep(bool scheduleState) {
            if (scheduleState && !IsScheduleRunning()) {
                StartScheduleThread(false);
            }
        }

        private void MainWindow_Load(object sender, EventArgs e) {
            // set up settings
            MySQLServerAddressInput.Text = Properties.Settings.Default.mySQLServerAddress;
            MySQLServerPortInput.Value = Convert.ToDecimal(Properties.Settings.Default.mySQLServerPort);
            MySQLServerDatabaseInput.Text = Properties.Settings.Default.mySQLServerDatabase;
            MySQLServerUsernameInput.Text = Properties.Settings.Default.mySQLServerUsername;
            MySQLServerPasswordInput.Text = DatabaseUtil.GetMySQLPassword();

            ArmA3MissionThresholdInput.Value = Properties.Settings.Default.missionThreshold;
            ArmA3PlayedThresholdInput.Value = Properties.Settings.Default.playedThreshold;
            ArmA3RunTimeThresholdInput.Value = ToSeconds(Properties.Settings.Default.runTimeThreshold);
            ArmA3ServerAddressInput.Text = Properties.Settings.Default.armaServerAddress;
            ArmA3ServerPollRateInput.Value = ToSeconds(Properties.Settings.Default.pollRate);
            ArmA3ServerPortInput.Value = Properties.Settings.Default.armaServerPort;
            serverReconnectLimitInput.Value = ToSeconds(Properties.Settings.Default.retryTimeLimit);

            // set up scheduler
            scheduleBindingSource.Clear();

            MySqlConnection connection = null;
            try {
                connection = DatabaseUtil.OpenDataSource();

                foreach (Schedule scheduleItem in new ScheduleDAO(connection).GetScheduleItems()) {
                    scheduleBindingSource.Add(scheduleItem);
                }
            } catch (MySqlException ex) {
                _logger.Error("Problem getting schedule items", ex);
            } finally {
                if (connection != null) {
                    connection.Dispose();
                }
            }
        }

        private void MainWindow_FormClosed(object sender, FormClosedEventArgs e) {
            if (_closeThreads) {
                StopScheduleThread();
            }
        }

        private void ScheduleInput_Click(object sender, EventArgs e) {
            if (IsScheduleRunning()) {
                StopScheduleThread();
            } else {
                StartScheduleThread(false);
            }
        }

        private int ToSeconds(int milliseconds) {
            return milliseconds / 1000;
        }

        private int ToMilliseconds(decimal seconds) {
            return (int)seconds * 1000;
        }

        private void Save_Click(object sender, EventArgs e) {
            bool scheduleState = PreSaveStepAndState();

            Properties.Settings.Default.mySQLServerAddress = MySQLServerAddressInput.Text;
            Properties.Settings.Default.mySQLServerPort = Convert.ToString(MySQLServerPortInput.Value);
            Properties.Settings.Default.mySQLServerDatabase = MySQLServerDatabaseInput.Text;
            Properties.Settings.Default.mySQLServerUsername = MySQLServerUsernameInput.Text;
            DatabaseUtil.SetMySQLPassword(MySQLServerPasswordInput.Text);
            Properties.Settings.Default.missionThreshold = (int)ArmA3MissionThresholdInput.Value;
            Properties.Settings.Default.playedThreshold = (int)ArmA3PlayedThresholdInput.Value;
            Properties.Settings.Default.runTimeThreshold = ToMilliseconds(ArmA3RunTimeThresholdInput.Value);
            Properties.Settings.Default.armaServerAddress = ArmA3ServerAddressInput.Text;
            Properties.Settings.Default.pollRate = ToMilliseconds(ArmA3ServerPollRateInput.Value);
            Properties.Settings.Default.armaServerPort = (int)ArmA3ServerPortInput.Value;
            Properties.Settings.Default.retryTimeLimit = ToMilliseconds(serverReconnectLimitInput.Value);

            Properties.Settings.Default.Save();
            PostSaveStep(scheduleState);

            MainWindow_Load(sender, e);
        }

        private void Cancel_Click(object sender, EventArgs e) {
            MainWindow_Load(sender, e);
        }

        private void ScheduleGrid_CancelRowEdit(object sender, QuestionEventArgs e) {
            MainWindow_Load(sender, e);
        }

        private void ScheduleGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e) {
            scheduleBindingSource.Position = e.RowIndex;

            bool scheduleState = PreSaveStepAndState();

            MySqlConnection connection = null;
            try {
                connection = DatabaseUtil.OpenDataSource();
                new ScheduleDAO(connection).SaveScheduleItem((Schedule)scheduleBindingSource.Current);
            } catch (MySqlException ex) {
                _logger.Error("Problem saving schedule items", ex);
            } finally {
                if (connection != null) {
                    connection.Dispose();
                }
            }

            PostSaveStep(scheduleState);
            MainWindow_Load(sender, e);
        }

        private bool IsReportRunning() {
            return _reportingJob.IsReporting;
        }

        private bool IsScheduleRunning() {
            return _reportingThread.IsAlive;
        }

        private void ScheduleGrid_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e) {
            bool scheduleState = PreSaveStepAndState();

            MySqlConnection connection = null;
            try {
                connection = DatabaseUtil.OpenDataSource();
                new ScheduleDAO(connection).RemoveScheduleItem((Schedule)e.Row.DataBoundItem);
            } catch (MySqlException ex) {
                _logger.Error("Problem removing schedule items", ex);
            } finally {
                if (connection != null) {
                    connection.Dispose();
                }
            }

            PostSaveStep(scheduleState);
        }

        private void CloseInput_Click(object sender, EventArgs e) {
            _closeThreads = false;
            Close();
        }

        private void StatusTimer_Tick(object sender, EventArgs e) {
            CheckThreadStatuses();
        }

        private void ReportingInput_Click(object sender, EventArgs e) {
            if (_reportingThread == null || !_reportingThread.IsAlive) { // Job has been halted
                StartScheduleThread(true);
            } else {
                if (IsReportRunning()) { // Report running
                    StopScheduleThread();
                    StartScheduleThread(false);
                } else { // waiting for next report run
                    StopScheduleThread();
                    StartScheduleThread(true);
                }
            }
        }

        private void ScheduleGrid_CellValidating(object sender, DataGridViewCellValidatingEventArgs e) {
            ScheduleGrid.Rows[e.RowIndex].ErrorText = "";

            if (ScheduleGrid.Rows[e.RowIndex].IsNewRow) {
                return;
            }

            if (e.ColumnIndex == 2) {
                Regex validation = new Regex(_TIME_VALIDATION);
                if (!validation.IsMatch((string)e.FormattedValue)) {
                    e.Cancel = true;
                    ScheduleGrid.Rows[e.RowIndex].ErrorText = "The time of day value must be in 'HH:mm:ss' format";
                }
            }
        }
    }
}
