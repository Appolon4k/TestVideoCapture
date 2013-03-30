namespace TestVideoCapture
{
    partial class MainForm
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
            this.panelVideoPreview = new System.Windows.Forms.Panel();
            this.buttonStart = new System.Windows.Forms.Button();
            this.buttonScreenshot = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // panelVideoPreview
            // 
            this.panelVideoPreview.Location = new System.Drawing.Point(12, 12);
            this.panelVideoPreview.Name = "panelVideoPreview";
            this.panelVideoPreview.Size = new System.Drawing.Size(313, 216);
            this.panelVideoPreview.TabIndex = 0;
            this.panelVideoPreview.Resize += new System.EventHandler(this.panelVideoPreview_Resize);
            // 
            // buttonStart
            // 
            this.buttonStart.Location = new System.Drawing.Point(331, 12);
            this.buttonStart.Name = "buttonStart";
            this.buttonStart.Size = new System.Drawing.Size(75, 23);
            this.buttonStart.TabIndex = 1;
            this.buttonStart.Text = "Запись";
            this.buttonStart.UseVisualStyleBackColor = true;
            this.buttonStart.Click += new System.EventHandler(this.buttonStart_Click);
            // 
            // buttonScreenshot
            // 
            this.buttonScreenshot.Location = new System.Drawing.Point(331, 41);
            this.buttonScreenshot.Name = "buttonScreenshot";
            this.buttonScreenshot.Size = new System.Drawing.Size(75, 23);
            this.buttonScreenshot.TabIndex = 3;
            this.buttonScreenshot.Text = "Скриншот";
            this.buttonScreenshot.UseVisualStyleBackColor = true;
            this.buttonScreenshot.Click += new System.EventHandler(this.buttonScreenshot_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(417, 242);
            this.Controls.Add(this.buttonScreenshot);
            this.Controls.Add(this.buttonStart);
            this.Controls.Add(this.panelVideoPreview);
            this.Name = "MainForm";
            this.Text = "Simple record application";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelVideoPreview;
        private System.Windows.Forms.Button buttonStart;
        private System.Windows.Forms.Button buttonScreenshot;
    }
}

