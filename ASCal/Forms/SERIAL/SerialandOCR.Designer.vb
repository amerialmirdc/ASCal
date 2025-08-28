<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class FrmMain
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
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
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.components = New System.ComponentModel.Container()
        Me.KryptonWebBrowser1 = New Krypton.Toolkit.KryptonWebBrowser()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.CmbPort = New System.Windows.Forms.ComboBox()
        Me.CmbBaud = New System.Windows.Forms.ComboBox()
        Me.BtnConnect = New System.Windows.Forms.Button()
        Me.BtnDisconnect = New System.Windows.Forms.Button()
        Me.GroupBox1 = New System.Windows.Forms.GroupBox()
        Me.BtnSend = New System.Windows.Forms.Button()
        Me.txtTransmit = New System.Windows.Forms.TextBox()
        Me.GroupBox2 = New System.Windows.Forms.GroupBox()
        Me.rtbReceived = New System.Windows.Forms.RichTextBox()
        Me.SerialPort1 = New System.IO.Ports.SerialPort(Me.components)
        Me.PictureBox1 = New System.Windows.Forms.PictureBox()
        Me.BtnCapture = New System.Windows.Forms.Button()
        Me.Button1 = New System.Windows.Forms.Button()
        Me.RichTextBox1 = New System.Windows.Forms.RichTextBox()
        Me.Label3 = New System.Windows.Forms.Label()
        Me.Dmmtxtbrand = New System.Windows.Forms.TextBox()
        Me.Label4 = New System.Windows.Forms.Label()
        Me.DMMtxtpartnumber = New System.Windows.Forms.TextBox()
        Me.Label5 = New System.Windows.Forms.Label()
        Me.DMMtxtparameter = New System.Windows.Forms.TextBox()
        Me.Label6 = New System.Windows.Forms.Label()
        Me.DMMtxtread = New System.Windows.Forms.TextBox()
        Me.ButtonDisable = New System.Windows.Forms.Button()
        Me.GroupBox1.SuspendLayout()
        Me.GroupBox2.SuspendLayout()
        CType(Me.PictureBox1, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'KryptonWebBrowser1
        '
        Me.KryptonWebBrowser1.Dock = System.Windows.Forms.DockStyle.Fill
        Me.KryptonWebBrowser1.Location = New System.Drawing.Point(0, 0)
        Me.KryptonWebBrowser1.Name = "KryptonWebBrowser1"
        Me.KryptonWebBrowser1.Size = New System.Drawing.Size(2564, 1415)
        Me.KryptonWebBrowser1.TabIndex = 0
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(63, 31)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(65, 16)
        Me.Label1.TabIndex = 1
        Me.Label1.Text = "Com Port:"
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Location = New System.Drawing.Point(63, 72)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(74, 16)
        Me.Label2.TabIndex = 2
        Me.Label2.Text = "Baud Rate:"
        '
        'CmbPort
        '
        Me.CmbPort.FormattingEnabled = True
        Me.CmbPort.Location = New System.Drawing.Point(166, 22)
        Me.CmbPort.Name = "CmbPort"
        Me.CmbPort.Size = New System.Drawing.Size(219, 24)
        Me.CmbPort.TabIndex = 3
        '
        'CmbBaud
        '
        Me.CmbBaud.FormattingEnabled = True
        Me.CmbBaud.Location = New System.Drawing.Point(166, 69)
        Me.CmbBaud.Name = "CmbBaud"
        Me.CmbBaud.Size = New System.Drawing.Size(219, 24)
        Me.CmbBaud.TabIndex = 4
        '
        'BtnConnect
        '
        Me.BtnConnect.Location = New System.Drawing.Point(415, 23)
        Me.BtnConnect.Name = "BtnConnect"
        Me.BtnConnect.Size = New System.Drawing.Size(96, 23)
        Me.BtnConnect.TabIndex = 5
        Me.BtnConnect.Text = "Connect"
        Me.BtnConnect.UseVisualStyleBackColor = True
        '
        'BtnDisconnect
        '
        Me.BtnDisconnect.Location = New System.Drawing.Point(415, 72)
        Me.BtnDisconnect.Name = "BtnDisconnect"
        Me.BtnDisconnect.Size = New System.Drawing.Size(96, 23)
        Me.BtnDisconnect.TabIndex = 6
        Me.BtnDisconnect.Text = "Disconnect"
        Me.BtnDisconnect.UseVisualStyleBackColor = True
        '
        'GroupBox1
        '
        Me.GroupBox1.Controls.Add(Me.BtnSend)
        Me.GroupBox1.Controls.Add(Me.txtTransmit)
        Me.GroupBox1.Location = New System.Drawing.Point(66, 127)
        Me.GroupBox1.Name = "GroupBox1"
        Me.GroupBox1.Size = New System.Drawing.Size(445, 72)
        Me.GroupBox1.TabIndex = 7
        Me.GroupBox1.TabStop = False
        Me.GroupBox1.Text = "Transmit Data"
        '
        'BtnSend
        '
        Me.BtnSend.Location = New System.Drawing.Point(349, 30)
        Me.BtnSend.Name = "BtnSend"
        Me.BtnSend.Size = New System.Drawing.Size(75, 23)
        Me.BtnSend.TabIndex = 9
        Me.BtnSend.Text = "Send"
        Me.BtnSend.UseVisualStyleBackColor = True
        '
        'txtTransmit
        '
        Me.txtTransmit.Location = New System.Drawing.Point(16, 31)
        Me.txtTransmit.Name = "txtTransmit"
        Me.txtTransmit.Size = New System.Drawing.Size(313, 22)
        Me.txtTransmit.TabIndex = 0
        '
        'GroupBox2
        '
        Me.GroupBox2.Controls.Add(Me.rtbReceived)
        Me.GroupBox2.Location = New System.Drawing.Point(66, 232)
        Me.GroupBox2.Name = "GroupBox2"
        Me.GroupBox2.Size = New System.Drawing.Size(445, 130)
        Me.GroupBox2.TabIndex = 8
        Me.GroupBox2.TabStop = False
        Me.GroupBox2.Text = "Received Data"
        '
        'rtbReceived
        '
        Me.rtbReceived.Location = New System.Drawing.Point(16, 21)
        Me.rtbReceived.Name = "rtbReceived"
        Me.rtbReceived.Size = New System.Drawing.Size(408, 96)
        Me.rtbReceived.TabIndex = 0
        Me.rtbReceived.Text = ""
        '
        'SerialPort1
        '
        '
        'PictureBox1
        '
        Me.PictureBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.PictureBox1.Location = New System.Drawing.Point(1038, 31)
        Me.PictureBox1.Name = "PictureBox1"
        Me.PictureBox1.Size = New System.Drawing.Size(682, 301)
        Me.PictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage
        Me.PictureBox1.TabIndex = 9
        Me.PictureBox1.TabStop = False
        '
        'BtnCapture
        '
        Me.BtnCapture.Location = New System.Drawing.Point(1681, 402)
        Me.BtnCapture.Name = "BtnCapture"
        Me.BtnCapture.Size = New System.Drawing.Size(161, 23)
        Me.BtnCapture.TabIndex = 13
        Me.BtnCapture.Text = "Capture"
        Me.BtnCapture.UseVisualStyleBackColor = True
        '
        'Button1
        '
        Me.Button1.Location = New System.Drawing.Point(821, 401)
        Me.Button1.Name = "Button1"
        Me.Button1.Size = New System.Drawing.Size(75, 23)
        Me.Button1.TabIndex = 14
        Me.Button1.Text = "Button1"
        Me.Button1.UseVisualStyleBackColor = True
        '
        'RichTextBox1
        '
        Me.RichTextBox1.Location = New System.Drawing.Point(27, 380)
        Me.RichTextBox1.Name = "RichTextBox1"
        Me.RichTextBox1.Size = New System.Drawing.Size(706, 100)
        Me.RichTextBox1.TabIndex = 1
        Me.RichTextBox1.Text = ""
        '
        'Label3
        '
        Me.Label3.AutoSize = True
        Me.Label3.Location = New System.Drawing.Point(24, 516)
        Me.Label3.Name = "Label3"
        Me.Label3.Size = New System.Drawing.Size(81, 16)
        Me.Label3.TabIndex = 15
        Me.Label3.Text = "DMM Brand:"
        '
        'Dmmtxtbrand
        '
        Me.Dmmtxtbrand.Location = New System.Drawing.Point(111, 510)
        Me.Dmmtxtbrand.Name = "Dmmtxtbrand"
        Me.Dmmtxtbrand.ReadOnly = True
        Me.Dmmtxtbrand.Size = New System.Drawing.Size(168, 22)
        Me.Dmmtxtbrand.TabIndex = 10
        '
        'Label4
        '
        Me.Label4.AutoSize = True
        Me.Label4.Location = New System.Drawing.Point(24, 560)
        Me.Label4.Name = "Label4"
        Me.Label4.Size = New System.Drawing.Size(79, 16)
        Me.Label4.TabIndex = 16
        Me.Label4.Text = "DMM Part #:"
        '
        'DMMtxtpartnumber
        '
        Me.DMMtxtpartnumber.Location = New System.Drawing.Point(111, 555)
        Me.DMMtxtpartnumber.Name = "DMMtxtpartnumber"
        Me.DMMtxtpartnumber.ReadOnly = True
        Me.DMMtxtpartnumber.Size = New System.Drawing.Size(168, 22)
        Me.DMMtxtpartnumber.TabIndex = 17
        '
        'Label5
        '
        Me.Label5.AutoSize = True
        Me.Label5.Location = New System.Drawing.Point(304, 513)
        Me.Label5.Name = "Label5"
        Me.Label5.Size = New System.Drawing.Size(108, 16)
        Me.Label5.TabIndex = 18
        Me.Label5.Text = "DMM Parameter:"
        '
        'DMMtxtparameter
        '
        Me.DMMtxtparameter.Location = New System.Drawing.Point(418, 510)
        Me.DMMtxtparameter.Name = "DMMtxtparameter"
        Me.DMMtxtparameter.ReadOnly = True
        Me.DMMtxtparameter.Size = New System.Drawing.Size(168, 22)
        Me.DMMtxtparameter.TabIndex = 19
        '
        'Label6
        '
        Me.Label6.AutoSize = True
        Me.Label6.Location = New System.Drawing.Point(304, 560)
        Me.Label6.Name = "Label6"
        Me.Label6.Size = New System.Drawing.Size(97, 16)
        Me.Label6.TabIndex = 20
        Me.Label6.Text = "DMM Reading:"
        '
        'DMMtxtread
        '
        Me.DMMtxtread.Location = New System.Drawing.Point(418, 554)
        Me.DMMtxtread.Name = "DMMtxtread"
        Me.DMMtxtread.ReadOnly = True
        Me.DMMtxtread.Size = New System.Drawing.Size(168, 22)
        Me.DMMtxtread.TabIndex = 21
        '
        'ButtonDisable
        '
        Me.ButtonDisable.Location = New System.Drawing.Point(1065, 401)
        Me.ButtonDisable.Name = "ButtonDisable"
        Me.ButtonDisable.Size = New System.Drawing.Size(75, 23)
        Me.ButtonDisable.TabIndex = 22
        Me.ButtonDisable.Text = "Button2"
        Me.ButtonDisable.UseVisualStyleBackColor = True
        '
        'FrmMain
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(8.0!, 16.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(2564, 1415)
        Me.Controls.Add(Me.ButtonDisable)
        Me.Controls.Add(Me.DMMtxtread)
        Me.Controls.Add(Me.Label6)
        Me.Controls.Add(Me.DMMtxtparameter)
        Me.Controls.Add(Me.Label5)
        Me.Controls.Add(Me.DMMtxtpartnumber)
        Me.Controls.Add(Me.Label4)
        Me.Controls.Add(Me.Dmmtxtbrand)
        Me.Controls.Add(Me.Label3)
        Me.Controls.Add(Me.RichTextBox1)
        Me.Controls.Add(Me.Button1)
        Me.Controls.Add(Me.BtnCapture)
        Me.Controls.Add(Me.PictureBox1)
        Me.Controls.Add(Me.GroupBox2)
        Me.Controls.Add(Me.GroupBox1)
        Me.Controls.Add(Me.BtnDisconnect)
        Me.Controls.Add(Me.BtnConnect)
        Me.Controls.Add(Me.CmbBaud)
        Me.Controls.Add(Me.CmbPort)
        Me.Controls.Add(Me.Label2)
        Me.Controls.Add(Me.Label1)
        Me.Controls.Add(Me.KryptonWebBrowser1)
        Me.Name = "FrmMain"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "Form1"
        Me.TopMost = True
        Me.GroupBox1.ResumeLayout(False)
        Me.GroupBox1.PerformLayout()
        Me.GroupBox2.ResumeLayout(False)
        CType(Me.PictureBox1, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents KryptonWebBrowser1 As Krypton.Toolkit.KryptonWebBrowser
    Friend WithEvents Label1 As Label
    Friend WithEvents Label2 As Label
    Friend WithEvents CmbPort As ComboBox
    Friend WithEvents CmbBaud As ComboBox
    Friend WithEvents BtnConnect As Button
    Friend WithEvents BtnDisconnect As Button
    Friend WithEvents GroupBox1 As GroupBox
    Friend WithEvents BtnSend As Button
    Friend WithEvents txtTransmit As TextBox
    Friend WithEvents GroupBox2 As GroupBox
    Friend WithEvents rtbReceived As RichTextBox
    Friend WithEvents SerialPort1 As IO.Ports.SerialPort
    Friend WithEvents PictureBox1 As PictureBox
    Friend WithEvents BtnCapture As Button
    Friend WithEvents Button1 As Button
    Friend WithEvents RichTextBox1 As RichTextBox
    Friend WithEvents Label3 As Label
    Friend WithEvents Dmmtxtbrand As TextBox
    Friend WithEvents Label4 As Label
    Friend WithEvents DMMtxtpartnumber As TextBox
    Friend WithEvents Label5 As Label
    Friend WithEvents DMMtxtparameter As TextBox
    Friend WithEvents Label6 As Label
    Friend WithEvents DMMtxtread As TextBox
    Friend WithEvents ButtonDisable As Button
End Class
