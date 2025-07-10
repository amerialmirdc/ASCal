<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class jobPreview
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.Panel1 = New System.Windows.Forms.Panel()
        Me.Panel8 = New System.Windows.Forms.Panel()
        Me.logoutBtn = New System.Windows.Forms.Button()
        Me.Panel4 = New System.Windows.Forms.Panel()
        Me.jobDashBtn = New System.Windows.Forms.Button()
        Me.Panel3 = New System.Windows.Forms.Panel()
        Me.calibrateBtn = New System.Windows.Forms.Button()
        Me.Panel2 = New System.Windows.Forms.Panel()
        Me.logoBtn = New System.Windows.Forms.PictureBox()
        Me.Panel5 = New System.Windows.Forms.Panel()
        Me.Panel1.SuspendLayout()
        Me.Panel8.SuspendLayout()
        Me.Panel4.SuspendLayout()
        Me.Panel3.SuspendLayout()
        Me.Panel2.SuspendLayout()
        CType(Me.logoBtn, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'Panel1
        '
        Me.Panel1.BackColor = System.Drawing.Color.Cyan
        Me.Panel1.Controls.Add(Me.Panel8)
        Me.Panel1.Controls.Add(Me.Panel4)
        Me.Panel1.Controls.Add(Me.Panel3)
        Me.Panel1.Controls.Add(Me.Panel2)
        Me.Panel1.Cursor = System.Windows.Forms.Cursors.Default
        Me.Panel1.Dock = System.Windows.Forms.DockStyle.Left
        Me.Panel1.Location = New System.Drawing.Point(0, 0)
        Me.Panel1.Name = "Panel1"
        Me.Panel1.Size = New System.Drawing.Size(300, 991)
        Me.Panel1.TabIndex = 23
        '
        'Panel8
        '
        Me.Panel8.Controls.Add(Me.logoutBtn)
        Me.Panel8.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.Panel8.Location = New System.Drawing.Point(0, 923)
        Me.Panel8.Name = "Panel8"
        Me.Panel8.Size = New System.Drawing.Size(300, 68)
        Me.Panel8.TabIndex = 5
        '
        'logoutBtn
        '
        Me.logoutBtn.BackColor = System.Drawing.Color.Cyan
        Me.logoutBtn.Cursor = System.Windows.Forms.Cursors.Hand
        Me.logoutBtn.FlatAppearance.BorderColor = System.Drawing.Color.Black
        Me.logoutBtn.FlatAppearance.MouseDownBackColor = System.Drawing.Color.White
        Me.logoutBtn.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Aqua
        Me.logoutBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.logoutBtn.Font = New System.Drawing.Font("Courier10 BT", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.logoutBtn.Location = New System.Drawing.Point(32, 16)
        Me.logoutBtn.Name = "logoutBtn"
        Me.logoutBtn.Size = New System.Drawing.Size(245, 41)
        Me.logoutBtn.TabIndex = 0
        Me.logoutBtn.Text = "LOGOUT"
        Me.logoutBtn.UseVisualStyleBackColor = False
        '
        'Panel4
        '
        Me.Panel4.Controls.Add(Me.jobDashBtn)
        Me.Panel4.Dock = System.Windows.Forms.DockStyle.Top
        Me.Panel4.Location = New System.Drawing.Point(0, 168)
        Me.Panel4.Name = "Panel4"
        Me.Panel4.Size = New System.Drawing.Size(300, 68)
        Me.Panel4.TabIndex = 2
        '
        'jobDashBtn
        '
        Me.jobDashBtn.Cursor = System.Windows.Forms.Cursors.Hand
        Me.jobDashBtn.FlatAppearance.BorderColor = System.Drawing.Color.Black
        Me.jobDashBtn.FlatAppearance.MouseDownBackColor = System.Drawing.Color.White
        Me.jobDashBtn.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Aqua
        Me.jobDashBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.jobDashBtn.Font = New System.Drawing.Font("Courier10 BT", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.jobDashBtn.Location = New System.Drawing.Point(32, 16)
        Me.jobDashBtn.Name = "jobDashBtn"
        Me.jobDashBtn.Size = New System.Drawing.Size(245, 41)
        Me.jobDashBtn.TabIndex = 0
        Me.jobDashBtn.Text = "JOB DASHBOARD"
        Me.jobDashBtn.UseVisualStyleBackColor = True
        '
        'Panel3
        '
        Me.Panel3.Controls.Add(Me.calibrateBtn)
        Me.Panel3.Dock = System.Windows.Forms.DockStyle.Top
        Me.Panel3.Location = New System.Drawing.Point(0, 100)
        Me.Panel3.Name = "Panel3"
        Me.Panel3.Size = New System.Drawing.Size(300, 68)
        Me.Panel3.TabIndex = 1
        '
        'calibrateBtn
        '
        Me.calibrateBtn.BackColor = System.Drawing.Color.Cyan
        Me.calibrateBtn.Cursor = System.Windows.Forms.Cursors.Default
        Me.calibrateBtn.Enabled = False
        Me.calibrateBtn.FlatAppearance.BorderColor = System.Drawing.Color.Black
        Me.calibrateBtn.FlatAppearance.MouseDownBackColor = System.Drawing.Color.White
        Me.calibrateBtn.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Aqua
        Me.calibrateBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.calibrateBtn.Font = New System.Drawing.Font("Courier10 BT", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.calibrateBtn.Location = New System.Drawing.Point(32, 16)
        Me.calibrateBtn.Name = "calibrateBtn"
        Me.calibrateBtn.Size = New System.Drawing.Size(245, 41)
        Me.calibrateBtn.TabIndex = 0
        Me.calibrateBtn.Text = "CALIBRATE"
        Me.calibrateBtn.UseVisualStyleBackColor = False
        '
        'Panel2
        '
        Me.Panel2.Controls.Add(Me.logoBtn)
        Me.Panel2.Dock = System.Windows.Forms.DockStyle.Top
        Me.Panel2.Location = New System.Drawing.Point(0, 0)
        Me.Panel2.Name = "Panel2"
        Me.Panel2.Size = New System.Drawing.Size(300, 100)
        Me.Panel2.TabIndex = 0
        '
        'logoBtn
        '
        Me.logoBtn.Cursor = System.Windows.Forms.Cursors.Hand
        Me.logoBtn.Image = Global.ASCal.My.Resources.Resources._31
        Me.logoBtn.Location = New System.Drawing.Point(56, 24)
        Me.logoBtn.Name = "logoBtn"
        Me.logoBtn.Size = New System.Drawing.Size(192, 66)
        Me.logoBtn.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage
        Me.logoBtn.TabIndex = 0
        Me.logoBtn.TabStop = False
        '
        'Panel5
        '
        Me.Panel5.Dock = System.Windows.Forms.DockStyle.Fill
        Me.Panel5.Location = New System.Drawing.Point(300, 0)
        Me.Panel5.Name = "Panel5"
        Me.Panel5.Size = New System.Drawing.Size(1602, 991)
        Me.Panel5.TabIndex = 24
        '
        'jobPreview
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1902, 991)
        Me.Controls.Add(Me.Panel5)
        Me.Controls.Add(Me.Panel1)
        Me.Name = "jobPreview"
        Me.Text = "Form1"
        Me.Panel1.ResumeLayout(False)
        Me.Panel8.ResumeLayout(False)
        Me.Panel4.ResumeLayout(False)
        Me.Panel3.ResumeLayout(False)
        Me.Panel2.ResumeLayout(False)
        CType(Me.logoBtn, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)

    End Sub
    Friend WithEvents Panel1 As System.Windows.Forms.Panel
    Friend WithEvents Panel8 As System.Windows.Forms.Panel
    Friend WithEvents logoutBtn As System.Windows.Forms.Button
    Friend WithEvents Panel4 As System.Windows.Forms.Panel
    Friend WithEvents jobDashBtn As System.Windows.Forms.Button
    Friend WithEvents Panel3 As System.Windows.Forms.Panel
    Friend WithEvents calibrateBtn As System.Windows.Forms.Button
    Friend WithEvents Panel2 As System.Windows.Forms.Panel
    Friend WithEvents logoBtn As System.Windows.Forms.PictureBox
    Friend WithEvents Panel5 As System.Windows.Forms.Panel
End Class
