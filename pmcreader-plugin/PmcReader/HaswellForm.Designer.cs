namespace PmcReader
{
    partial class HaswellForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.configSelect = new System.Windows.Forms.ListView();
            this.applyConfigButton = new System.Windows.Forms.Button();
            this.monitoringListView = new System.Windows.Forms.ListView();
            this.cpuidLabel = new System.Windows.Forms.Label();
            this.configListLabel = new System.Windows.Forms.Label();
            this.errorLabel = new System.Windows.Forms.Label();
            this.L3ConfigSelect = new System.Windows.Forms.ListView();
            this.L3CacheConfigLabel = new System.Windows.Forms.Label();
            this.applyL3ConfigButton = new System.Windows.Forms.Button();
            this.L3MonitoringListView = new System.Windows.Forms.ListView();
            this.dfConfigSelect = new System.Windows.Forms.ListView();
            this.DataFabricConfigLabel = new System.Windows.Forms.Label();
            this.applyDfConfigButton = new System.Windows.Forms.Button();
            this.dfMonitoringListView = new System.Windows.Forms.ListView();
            this.l3ErrorMessage = new System.Windows.Forms.Label();
            this.logButton = new System.Windows.Forms.Button();
            this.logFilePathTextBox = new System.Windows.Forms.TextBox();
            this.stopLoggingButton = new System.Windows.Forms.Button();
            this.logFilePathLabel = new System.Windows.Forms.Label();
            this.L3LogToFileButton = new System.Windows.Forms.Button();
            this.L3LogToFileTextBox = new System.Windows.Forms.TextBox();
            this.L3StopLoggingButton = new System.Windows.Forms.Button();
            this.DfStopLoggingButton = new System.Windows.Forms.Button();
            this.DfLogToFileButton = new System.Windows.Forms.Button();
            this.DfLogToFileTextBox = new System.Windows.Forms.TextBox();
            this.crazyThingsPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.crazyThingsLabel = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.RestrictCoreLoggingTextBox = new System.Windows.Forms.TextBox();
            this.RestrictCoreLogLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // configSelect
            // 
            this.configSelect.HideSelection = false;
            this.configSelect.Location = new System.Drawing.Point(12, 34);
            this.configSelect.MultiSelect = false;
            this.configSelect.Name = "configSelect";
            this.configSelect.Size = new System.Drawing.Size(682, 85);
            this.configSelect.TabIndex = 1;
            this.configSelect.UseCompatibleStateImageBehavior = false;
            this.configSelect.View = System.Windows.Forms.View.List;
            this.configSelect.SelectedIndexChanged += new System.EventHandler(this.listView1_SelectedIndexChanged);
            // 
            // applyConfigButton
            // 
            this.applyConfigButton.Location = new System.Drawing.Point(12, 125);
            this.applyConfigButton.Name = "applyConfigButton";
            this.applyConfigButton.Size = new System.Drawing.Size(75, 23);
            this.applyConfigButton.TabIndex = 2;
            this.applyConfigButton.Text = "Apply Config";
            this.applyConfigButton.UseVisualStyleBackColor = true;
            this.applyConfigButton.Click += new System.EventHandler(this.applyConfigButton_Click);
            // 
            // monitoringListView
            // 
            this.monitoringListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.monitoringListView.HideSelection = false;
            this.monitoringListView.Location = new System.Drawing.Point(12, 154);
            this.monitoringListView.Name = "monitoringListView";
            this.monitoringListView.Size = new System.Drawing.Size(1004, 284);
            this.monitoringListView.TabIndex = 3;
            this.monitoringListView.UseCompatibleStateImageBehavior = false;
            this.monitoringListView.View = System.Windows.Forms.View.Details;
            // 
            // cpuidLabel
            // 
            this.cpuidLabel.AutoSize = true;
            this.cpuidLabel.Location = new System.Drawing.Point(9, 5);
            this.cpuidLabel.Name = "cpuidLabel";
            this.cpuidLabel.Size = new System.Drawing.Size(35, 13);
            this.cpuidLabel.TabIndex = 4;
            this.cpuidLabel.Text = "label1";
            // 
            // configListLabel
            // 
            this.configListLabel.AutoSize = true;
            this.configListLabel.Location = new System.Drawing.Point(9, 18);
            this.configListLabel.Name = "configListLabel";
            this.configListLabel.Size = new System.Drawing.Size(178, 13);
            this.configListLabel.TabIndex = 5;
            this.configListLabel.Text = "Core PMC Configurations (pick one):";
            // 
            // errorLabel
            // 
            this.errorLabel.AutoSize = true;
            this.errorLabel.Location = new System.Drawing.Point(193, 18);
            this.errorLabel.Name = "errorLabel";
            this.errorLabel.Size = new System.Drawing.Size(34, 13);
            this.errorLabel.TabIndex = 6;
            this.errorLabel.Text = "(error)";
            // 
            // L3ConfigSelect
            // 
            this.L3ConfigSelect.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.L3ConfigSelect.HideSelection = false;
            this.L3ConfigSelect.Location = new System.Drawing.Point(13, 457);
            this.L3ConfigSelect.MultiSelect = false;
            this.L3ConfigSelect.Name = "L3ConfigSelect";
            this.L3ConfigSelect.Size = new System.Drawing.Size(512, 63);
            this.L3ConfigSelect.TabIndex = 7;
            this.L3ConfigSelect.UseCompatibleStateImageBehavior = false;
            this.L3ConfigSelect.View = System.Windows.Forms.View.List;
            // 
            // L3CacheConfigLabel
            // 
            this.L3CacheConfigLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.L3CacheConfigLabel.AutoSize = true;
            this.L3CacheConfigLabel.Location = new System.Drawing.Point(10, 441);
            this.L3CacheConfigLabel.Name = "L3CacheConfigLabel";
            this.L3CacheConfigLabel.Size = new System.Drawing.Size(202, 13);
            this.L3CacheConfigLabel.TabIndex = 8;
            this.L3CacheConfigLabel.Text = "L3 Cache PMC Configurations (pick one):";
            // 
            // applyL3ConfigButton
            // 
            this.applyL3ConfigButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.applyL3ConfigButton.Location = new System.Drawing.Point(13, 527);
            this.applyL3ConfigButton.Name = "applyL3ConfigButton";
            this.applyL3ConfigButton.Size = new System.Drawing.Size(94, 23);
            this.applyL3ConfigButton.TabIndex = 9;
            this.applyL3ConfigButton.Text = "Apply L3 Config";
            this.applyL3ConfigButton.UseVisualStyleBackColor = true;
            this.applyL3ConfigButton.Click += new System.EventHandler(this.applyL3ConfigButton_Click);
            // 
            // L3MonitoringListView
            // 
            this.L3MonitoringListView.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.L3MonitoringListView.HideSelection = false;
            this.L3MonitoringListView.Location = new System.Drawing.Point(12, 557);
            this.L3MonitoringListView.Name = "L3MonitoringListView";
            this.L3MonitoringListView.Size = new System.Drawing.Size(513, 118);
            this.L3MonitoringListView.TabIndex = 10;
            this.L3MonitoringListView.UseCompatibleStateImageBehavior = false;
            this.L3MonitoringListView.View = System.Windows.Forms.View.Details;
            // 
            // dfConfigSelect
            // 
            this.dfConfigSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.dfConfigSelect.HideSelection = false;
            this.dfConfigSelect.Location = new System.Drawing.Point(531, 457);
            this.dfConfigSelect.MultiSelect = false;
            this.dfConfigSelect.Name = "dfConfigSelect";
            this.dfConfigSelect.Size = new System.Drawing.Size(485, 63);
            this.dfConfigSelect.TabIndex = 11;
            this.dfConfigSelect.UseCompatibleStateImageBehavior = false;
            this.dfConfigSelect.View = System.Windows.Forms.View.List;
            // 
            // DataFabricConfigLabel
            // 
            this.DataFabricConfigLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.DataFabricConfigLabel.AutoSize = true;
            this.DataFabricConfigLabel.Location = new System.Drawing.Point(528, 441);
            this.DataFabricConfigLabel.Name = "DataFabricConfigLabel";
            this.DataFabricConfigLabel.Size = new System.Drawing.Size(211, 13);
            this.DataFabricConfigLabel.TabIndex = 12;
            this.DataFabricConfigLabel.Text = "Data Fabric PMC Configurations (pick one):";
            // 
            // applyDfConfigButton
            // 
            this.applyDfConfigButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.applyDfConfigButton.Location = new System.Drawing.Point(531, 528);
            this.applyDfConfigButton.Name = "applyDfConfigButton";
            this.applyDfConfigButton.Size = new System.Drawing.Size(102, 23);
            this.applyDfConfigButton.TabIndex = 13;
            this.applyDfConfigButton.Text = "Apply DF Config";
            this.applyDfConfigButton.UseVisualStyleBackColor = true;
            this.applyDfConfigButton.Click += new System.EventHandler(this.applyDfConfigButton_Click);
            // 
            // dfMonitoringListView
            // 
            this.dfMonitoringListView.Activation = System.Windows.Forms.ItemActivation.OneClick;
            this.dfMonitoringListView.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.dfMonitoringListView.HideSelection = false;
            this.dfMonitoringListView.Location = new System.Drawing.Point(531, 557);
            this.dfMonitoringListView.Name = "dfMonitoringListView";
            this.dfMonitoringListView.Size = new System.Drawing.Size(485, 118);
            this.dfMonitoringListView.TabIndex = 14;
            this.dfMonitoringListView.UseCompatibleStateImageBehavior = false;
            this.dfMonitoringListView.View = System.Windows.Forms.View.Details;
            // 
            // l3ErrorMessage
            // 
            this.l3ErrorMessage.AutoSize = true;
            this.l3ErrorMessage.Location = new System.Drawing.Point(114, 536);
            this.l3ErrorMessage.Name = "l3ErrorMessage";
            this.l3ErrorMessage.Size = new System.Drawing.Size(0, 13);
            this.l3ErrorMessage.TabIndex = 15;
            // 
            // logButton
            // 
            this.logButton.Location = new System.Drawing.Point(402, 125);
            this.logButton.Name = "logButton";
            this.logButton.Size = new System.Drawing.Size(75, 23);
            this.logButton.TabIndex = 17;
            this.logButton.Text = "Log To File";
            this.logButton.UseVisualStyleBackColor = true;
            this.logButton.Click += new System.EventHandler(this.logButton_Click);
            // 
            // logFilePathTextBox
            // 
            this.logFilePathTextBox.Location = new System.Drawing.Point(178, 127);
            this.logFilePathTextBox.Name = "logFilePathTextBox";
            this.logFilePathTextBox.Size = new System.Drawing.Size(218, 20);
            this.logFilePathTextBox.TabIndex = 18;
            // 
            // stopLoggingButton
            // 
            this.stopLoggingButton.Location = new System.Drawing.Point(483, 125);
            this.stopLoggingButton.Name = "stopLoggingButton";
            this.stopLoggingButton.Size = new System.Drawing.Size(87, 23);
            this.stopLoggingButton.TabIndex = 19;
            this.stopLoggingButton.Text = "Stop Logging";
            this.stopLoggingButton.UseVisualStyleBackColor = true;
            this.stopLoggingButton.Click += new System.EventHandler(this.stopLoggingButton_Click);
            // 
            // logFilePathLabel
            // 
            this.logFilePathLabel.AutoSize = true;
            this.logFilePathLabel.Location = new System.Drawing.Point(103, 130);
            this.logFilePathLabel.Name = "logFilePathLabel";
            this.logFilePathLabel.Size = new System.Drawing.Size(69, 13);
            this.logFilePathLabel.TabIndex = 20;
            this.logFilePathLabel.Text = "Log File Path";
            // 
            // L3LogToFileButton
            // 
            this.L3LogToFileButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.L3LogToFileButton.Location = new System.Drawing.Point(527, 528);
            this.L3LogToFileButton.Name = "L3LogToFileButton";
            this.L3LogToFileButton.Size = new System.Drawing.Size(75, 23);
            this.L3LogToFileButton.TabIndex = 21;
            this.L3LogToFileButton.Text = "Log To File";
            this.L3LogToFileButton.UseVisualStyleBackColor = true;
            this.L3LogToFileButton.Click += new System.EventHandler(this.L3LogToFileButton_Click);
            // 
            // L3LogToFileTextBox
            // 
            this.L3LogToFileTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.L3LogToFileTextBox.Location = new System.Drawing.Point(313, 530);
            this.L3LogToFileTextBox.Name = "L3LogToFileTextBox";
            this.L3LogToFileTextBox.Size = new System.Drawing.Size(208, 20);
            this.L3LogToFileTextBox.TabIndex = 22;
            // 
            // L3StopLoggingButton
            // 
            this.L3StopLoggingButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.L3StopLoggingButton.Location = new System.Drawing.Point(608, 528);
            this.L3StopLoggingButton.Name = "L3StopLoggingButton";
            this.L3StopLoggingButton.Size = new System.Drawing.Size(86, 23);
            this.L3StopLoggingButton.TabIndex = 23;
            this.L3StopLoggingButton.Text = "Stop Logging";
            this.L3StopLoggingButton.UseVisualStyleBackColor = true;
            this.L3StopLoggingButton.Click += new System.EventHandler(this.L3StopLoggingButton_Click);
            // 
            // DfStopLoggingButton
            // 
            this.DfStopLoggingButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.DfStopLoggingButton.Location = new System.Drawing.Point(930, 528);
            this.DfStopLoggingButton.Name = "DfStopLoggingButton";
            this.DfStopLoggingButton.Size = new System.Drawing.Size(86, 23);
            this.DfStopLoggingButton.TabIndex = 24;
            this.DfStopLoggingButton.Text = "Stop Logging";
            this.DfStopLoggingButton.UseVisualStyleBackColor = true;
            this.DfStopLoggingButton.Click += new System.EventHandler(this.DfStopLoggingButton_Click);
            // 
            // DfLogToFileButton
            // 
            this.DfLogToFileButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.DfLogToFileButton.Location = new System.Drawing.Point(849, 528);
            this.DfLogToFileButton.Name = "DfLogToFileButton";
            this.DfLogToFileButton.Size = new System.Drawing.Size(75, 23);
            this.DfLogToFileButton.TabIndex = 25;
            this.DfLogToFileButton.Text = "Log To File";
            this.DfLogToFileButton.UseVisualStyleBackColor = true;
            this.DfLogToFileButton.Click += new System.EventHandler(this.DfLogToFileButton_Click);
            // 
            // DfLogToFileTextBox
            // 
            this.DfLogToFileTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.DfLogToFileTextBox.Location = new System.Drawing.Point(649, 530);
            this.DfLogToFileTextBox.Name = "DfLogToFileTextBox";
            this.DfLogToFileTextBox.Size = new System.Drawing.Size(194, 20);
            this.DfLogToFileTextBox.TabIndex = 26;
            // 
            // crazyThingsPanel
            // 
            this.crazyThingsPanel.Location = new System.Drawing.Point(700, 34);
            this.crazyThingsPanel.Name = "crazyThingsPanel";
            this.crazyThingsPanel.Size = new System.Drawing.Size(485, 114);
            this.crazyThingsPanel.TabIndex = 27;
            // 
            // crazyThingsLabel
            // 
            this.crazyThingsLabel.AutoSize = true;
            this.crazyThingsLabel.Location = new System.Drawing.Point(697, 18);
            this.crazyThingsLabel.Name = "crazyThingsLabel";
            this.crazyThingsLabel.Size = new System.Drawing.Size(0, 13);
            this.crazyThingsLabel.TabIndex = 28;
            // 
            // RestrictCoreLoggingTextBox
            // 
            this.RestrictCoreLoggingTextBox.Location = new System.Drawing.Point(655, 127);
            this.RestrictCoreLoggingTextBox.Name = "RestrictCoreLoggingTextBox";
            this.RestrictCoreLoggingTextBox.Size = new System.Drawing.Size(39, 20);
            this.RestrictCoreLoggingTextBox.TabIndex = 29;
            // 
            // RestrictCoreLogLabel
            // 
            this.RestrictCoreLogLabel.AutoSize = true;
            this.RestrictCoreLogLabel.Location = new System.Drawing.Point(576, 130);
            this.RestrictCoreLogLabel.Name = "RestrictCoreLogLabel";
            this.RestrictCoreLogLabel.Size = new System.Drawing.Size(72, 13);
            this.RestrictCoreLogLabel.TabIndex = 30;
            this.RestrictCoreLogLabel.Text = "Only log core:";
            // 
            // HaswellForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1028, 687);
            this.Controls.Add(this.RestrictCoreLogLabel);
            this.Controls.Add(this.RestrictCoreLoggingTextBox);
            this.Controls.Add(this.crazyThingsLabel);
            this.Controls.Add(this.crazyThingsPanel);
            this.Controls.Add(this.DfLogToFileTextBox);
            this.Controls.Add(this.DfLogToFileButton);
            this.Controls.Add(this.DfStopLoggingButton);
            this.Controls.Add(this.L3StopLoggingButton);
            this.Controls.Add(this.L3LogToFileTextBox);
            this.Controls.Add(this.L3LogToFileButton);
            this.Controls.Add(this.logFilePathLabel);
            this.Controls.Add(this.stopLoggingButton);
            this.Controls.Add(this.logFilePathTextBox);
            this.Controls.Add(this.logButton);
            this.Controls.Add(this.l3ErrorMessage);
            this.Controls.Add(this.dfMonitoringListView);
            this.Controls.Add(this.applyDfConfigButton);
            this.Controls.Add(this.DataFabricConfigLabel);
            this.Controls.Add(this.dfConfigSelect);
            this.Controls.Add(this.L3MonitoringListView);
            this.Controls.Add(this.applyL3ConfigButton);
            this.Controls.Add(this.L3CacheConfigLabel);
            this.Controls.Add(this.L3ConfigSelect);
            this.Controls.Add(this.errorLabel);
            this.Controls.Add(this.configListLabel);
            this.Controls.Add(this.cpuidLabel);
            this.Controls.Add(this.monitoringListView);
            this.Controls.Add(this.applyConfigButton);
            this.Controls.Add(this.configSelect);
            this.MinimumSize = new System.Drawing.Size(1022, 500);
            this.Name = "HaswellForm";
            this.Text = "Clam CPU Performance Monitoring (WIP)";
            this.Load += new System.EventHandler(this.HaswellForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ListView configSelect;
        private System.Windows.Forms.Button applyConfigButton;
        private System.Windows.Forms.ListView monitoringListView;
        private System.Windows.Forms.Label cpuidLabel;
        private System.Windows.Forms.Label configListLabel;
        private System.Windows.Forms.Label errorLabel;
        private System.Windows.Forms.ListView L3ConfigSelect;
        private System.Windows.Forms.Label L3CacheConfigLabel;
        private System.Windows.Forms.Button applyL3ConfigButton;
        private System.Windows.Forms.ListView L3MonitoringListView;
        private System.Windows.Forms.ListView dfConfigSelect;
        private System.Windows.Forms.Label DataFabricConfigLabel;
        private System.Windows.Forms.Button applyDfConfigButton;
        private System.Windows.Forms.ListView dfMonitoringListView;
        private System.Windows.Forms.Label l3ErrorMessage;
        private System.Windows.Forms.Button logButton;
        private System.Windows.Forms.TextBox logFilePathTextBox;
        private System.Windows.Forms.Button stopLoggingButton;
        private System.Windows.Forms.Label logFilePathLabel;
        private System.Windows.Forms.Button L3LogToFileButton;
        private System.Windows.Forms.TextBox L3LogToFileTextBox;
        private System.Windows.Forms.Button L3StopLoggingButton;
        private System.Windows.Forms.Button DfStopLoggingButton;
        private System.Windows.Forms.Button DfLogToFileButton;
        private System.Windows.Forms.TextBox DfLogToFileTextBox;
        private System.Windows.Forms.FlowLayoutPanel crazyThingsPanel;
        private System.Windows.Forms.Label crazyThingsLabel;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.TextBox RestrictCoreLoggingTextBox;
        private System.Windows.Forms.Label RestrictCoreLogLabel;
    }
}

