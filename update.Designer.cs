using System.Drawing;
using System.Resources;

namespace LineageII
{
    partial class update
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.titleBarPanel = new System.Windows.Forms.Panel();
            this.btnSettings = new System.Windows.Forms.Button();
            this.btnMinimize = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblSubtitle = new System.Windows.Forms.Label();
            this.footerPanel = new System.Windows.Forms.Panel();
            this.lblDownloadStatus = new System.Windows.Forms.Label();
            this.downloadProgress = new LineageII.Controls.DualProgressBar();
            this.patchProgress = new LineageII.Controls.DualProgressBar();
            this.lblStatus = new System.Windows.Forms.Label();
            this.browserHostPanel = new LineageII.Controls.RoundedPanel();
            this.browser = new Microsoft.Web.WebView2.WinForms.WebView2();
            this.titleBarPanel.SuspendLayout();
            this.footerPanel.SuspendLayout();
            this.browserHostPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.browser)).BeginInit();
            this.SuspendLayout();
            // 
            // titleBarPanel
            // 
            this.titleBarPanel.Controls.Add(this.btnSettings);
            this.titleBarPanel.Controls.Add(this.btnMinimize);
            this.titleBarPanel.Controls.Add(this.btnClose);
            this.titleBarPanel.Controls.Add(this.lblTitle);
            this.titleBarPanel.Controls.Add(this.lblSubtitle);
            this.titleBarPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.titleBarPanel.Location = new System.Drawing.Point(0, 0);
            this.titleBarPanel.Name = "titleBarPanel";
            this.titleBarPanel.Size = new System.Drawing.Size(1280, 64);
            this.titleBarPanel.TabIndex = 0;
            // 
            // btnSettings
            // 
            this.btnSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSettings.FlatAppearance.BorderSize = 0;
            this.btnSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSettings.Font = new System.Drawing.Font("Segoe UI Symbol", 16F);
            this.btnSettings.Location = new System.Drawing.Point(1144, 12);
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.Size = new System.Drawing.Size(40, 40);
            this.btnSettings.TabIndex = 3;
            this.btnSettings.Text = "⚙";
            this.btnSettings.UseVisualStyleBackColor = true;
            // 
            // btnMinimize
            // 
            this.btnMinimize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnMinimize.FlatAppearance.BorderSize = 0;
            this.btnMinimize.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnMinimize.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.btnMinimize.Location = new System.Drawing.Point(1192, 12);
            this.btnMinimize.Name = "btnMinimize";
            this.btnMinimize.Size = new System.Drawing.Size(34, 40);
            this.btnMinimize.TabIndex = 2;
            this.btnMinimize.Text = "—";
            this.btnMinimize.UseVisualStyleBackColor = true;
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.FlatAppearance.BorderSize = 0;
            this.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClose.Font = new System.Drawing.Font("Segoe UI", 13F, System.Drawing.FontStyle.Bold);
            this.btnClose.Location = new System.Drawing.Point(1233, 12);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(34, 40);
            this.btnClose.TabIndex = 1;
            this.btnClose.Text = "×";
            this.btnClose.UseVisualStyleBackColor = true;
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(444, 15);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(375, 32);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "LINEAGE II RP INTERLUDE";
            // 
            // lblSubtitle
            // 
            this.lblSubtitle.Location = new System.Drawing.Point(0, 0);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Size = new System.Drawing.Size(100, 23);
            this.lblSubtitle.TabIndex = 4;
            // 
            // footerPanel
            // 
            this.footerPanel.Controls.Add(this.lblDownloadStatus);
            this.footerPanel.Controls.Add(this.downloadProgress);
            this.footerPanel.Controls.Add(this.patchProgress);
            this.footerPanel.Controls.Add(this.lblStatus);
            this.footerPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.footerPanel.Location = new System.Drawing.Point(0, 637);
            this.footerPanel.Name = "footerPanel";
            this.footerPanel.Size = new System.Drawing.Size(1280, 83);
            this.footerPanel.TabIndex = 3;
            // 
            // lblDownloadStatus
            // 
            this.lblDownloadStatus.AutoSize = true;
            this.lblDownloadStatus.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblDownloadStatus.Location = new System.Drawing.Point(24, 62);
            this.lblDownloadStatus.Name = "lblDownloadStatus";
            this.lblDownloadStatus.Size = new System.Drawing.Size(110, 15);
            this.lblDownloadStatus.TabIndex = 3;
            this.lblDownloadStatus.Text = "Aguardando ação...";
            // 
            // downloadProgress
            // 
            this.downloadProgress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.downloadProgress.Caption = "Check";
            this.downloadProgress.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.downloadProgress.Location = new System.Drawing.Point(24, 37);
            this.downloadProgress.Name = "downloadProgress";
            this.downloadProgress.Size = new System.Drawing.Size(1232, 22);
            this.downloadProgress.TabIndex = 2;
            // 
            // patchProgress
            // 
            this.patchProgress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.patchProgress.Caption = "Atividade";
            this.patchProgress.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.patchProgress.Location = new System.Drawing.Point(24, 8);
            this.patchProgress.Name = "patchProgress";
            this.patchProgress.Size = new System.Drawing.Size(1232, 22);
            this.patchProgress.TabIndex = 1;
            // 
            // lblStatus
            // 
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.lblStatus.Location = new System.Drawing.Point(854, 60);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(402, 18);
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Text = "Pronto";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // browserHostPanel
            // 
            this.browserHostPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.browserHostPanel.BorderColor = System.Drawing.Color.Transparent;
            this.browserHostPanel.BorderThickness = 1;
            this.browserHostPanel.Controls.Add(this.browser);
            this.browserHostPanel.CornerRadius = 18;
            this.browserHostPanel.Location = new System.Drawing.Point(0, 70);
            this.browserHostPanel.Name = "browserHostPanel";
            this.browserHostPanel.Size = new System.Drawing.Size(1277, 550);
            this.browserHostPanel.TabIndex = 2;
            // 
            // browser
            // 
            this.browser.AllowExternalDrop = true;
            this.browser.CreationProperties = null;
            this.browser.DefaultBackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(5)))), ((int)(((byte)(4)))), ((int)(((byte)(5)))));
            this.browser.Dock = System.Windows.Forms.DockStyle.Fill;
            this.browser.Location = new System.Drawing.Point(0, 0);
            this.browser.MinimumSize = new System.Drawing.Size(20, 20);
            this.browser.Name = "browser";
            this.browser.Size = new System.Drawing.Size(1277, 550);
            this.browser.TabIndex = 0;
            this.browser.ZoomFactor = 1D;
            // 
            // update
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1280, 720);
            this.Controls.Add(this.footerPanel);
            this.Controls.Add(this.browserHostPanel);
            this.Controls.Add(this.titleBarPanel);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = global::LineageII.Properties.Resources.icone;
            this.Name = "update";
            this.Text = "Update2";
            this.titleBarPanel.ResumeLayout(false);
            this.titleBarPanel.PerformLayout();
            this.footerPanel.ResumeLayout(false);
            this.footerPanel.PerformLayout();
            this.browserHostPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.browser)).EndInit();
            this.ResumeLayout(false);

        }

        private System.Windows.Forms.Panel titleBarPanel;
        private System.Windows.Forms.Button btnSettings;
        private System.Windows.Forms.Button btnMinimize;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblSubtitle;
        private LineageII.Controls.RoundedPanel browserHostPanel;
        private Microsoft.Web.WebView2.WinForms.WebView2 browser;
        private System.Windows.Forms.Panel footerPanel;
        private System.Windows.Forms.Label lblDownloadStatus;
        private LineageII.Controls.DualProgressBar downloadProgress;
        private LineageII.Controls.DualProgressBar patchProgress;
        private System.Windows.Forms.Label lblStatus;
    }
}
