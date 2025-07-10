<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class landingPageSignatory
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
        Me.Button2 = New System.Windows.Forms.Button()
        Me.Panel2 = New System.Windows.Forms.Panel()
        Me.logoBox = New System.Windows.Forms.PictureBox()
        Me.Panel6 = New System.Windows.Forms.Panel()
        Me.pageLabel = New System.Windows.Forms.Label()
        Me.prevBtn = New System.Windows.Forms.Button()
        Me.nextBtn = New System.Windows.Forms.Button()
        Me.userJobLogs = New System.Windows.Forms.FlowLayoutPanel()
        Me.userLogs = New System.Windows.Forms.Label()
        Me.accountType = New System.Windows.Forms.Label()
        Me.Label5 = New System.Windows.Forms.Label()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.Label3 = New System.Windows.Forms.Label()
        Me.userDepartment = New System.Windows.Forms.Label()
        Me.Label6 = New System.Windows.Forms.Label()
        Me.userDesig = New System.Windows.Forms.Label()
        Me.userEmail = New System.Windows.Forms.Label()
        Me.userMobile = New System.Windows.Forms.Label()
        Me.userBirthday = New System.Windows.Forms.Label()
        Me.userName = New System.Windows.Forms.Label()
        Me.ShapeContainer1 = New Microsoft.VisualBasic.PowerPacks.ShapeContainer()
        Me.LineShape2 = New Microsoft.VisualBasic.PowerPacks.LineShape()
        Me.LineShape1 = New Microsoft.VisualBasic.PowerPacks.LineShape()
        Me.Panel1.SuspendLayout()
        Me.Panel8.SuspendLayout()
        Me.Panel4.SuspendLayout()
        Me.Panel2.SuspendLayout()
        CType(Me.logoBox, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.Panel6.SuspendLayout()
        Me.SuspendLayout()
        '
        'Panel1
        '
        Me.Panel1.BackColor = System.Drawing.Color.Cyan
        Me.Panel1.Controls.Add(Me.Panel8)
        Me.Panel1.Controls.Add(Me.Panel4)
        Me.Panel1.Controls.Add(Me.Panel2)
        Me.Panel1.Dock = System.Windows.Forms.DockStyle.Left
        Me.Panel1.Location = New System.Drawing.Point(0, 0)
        Me.Panel1.Name = "Panel1"
        Me.Panel1.Size = New System.Drawing.Size(350, 1061)
        Me.Panel1.TabIndex = 22
        '
        'Panel8
        '
        Me.Panel8.Controls.Add(Me.logoutBtn)
        Me.Panel8.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.Panel8.Location = New System.Drawing.Point(0, 993)
        Me.Panel8.Name = "Panel8"
        Me.Panel8.Size = New System.Drawing.Size(350, 68)
        Me.Panel8.TabIndex = 4
        '
        'logoutBtn
        '
        Me.logoutBtn.BackColor = System.Drawing.Color.Cyan
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
        Me.Panel4.Controls.Add(Me.Button2)
        Me.Panel4.Dock = System.Windows.Forms.DockStyle.Top
        Me.Panel4.Location = New System.Drawing.Point(0, 100)
        Me.Panel4.Name = "Panel4"
        Me.Panel4.Size = New System.Drawing.Size(350, 68)
        Me.Panel4.TabIndex = 2
        '
        'Button2
        '
        Me.Button2.FlatAppearance.BorderColor = System.Drawing.Color.Black
        Me.Button2.FlatAppearance.MouseDownBackColor = System.Drawing.Color.White
        Me.Button2.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Aqua
        Me.Button2.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.Button2.Font = New System.Drawing.Font("Courier10 BT", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Button2.Location = New System.Drawing.Point(32, 16)
        Me.Button2.Name = "Button2"
        Me.Button2.Size = New System.Drawing.Size(286, 38)
        Me.Button2.TabIndex = 0
        Me.Button2.Text = "JOB DASHBOARD"
        Me.Button2.UseVisualStyleBackColor = True
        '
        'Panel2
        '
        Me.Panel2.Controls.Add(Me.logoBox)
        Me.Panel2.Dock = System.Windows.Forms.DockStyle.Top
        Me.Panel2.Location = New System.Drawing.Point(0, 0)
        Me.Panel2.Name = "Panel2"
        Me.Panel2.Size = New System.Drawing.Size(350, 100)
        Me.Panel2.TabIndex = 0
        '
        'logoBox
        '
        Me.logoBox.Image = Global.ASCal.My.Resources.Resources._31
        Me.logoBox.Location = New System.Drawing.Point(65, 22)
        Me.logoBox.Name = "logoBox"
        Me.logoBox.Size = New System.Drawing.Size(224, 61)
        Me.logoBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage
        Me.logoBox.TabIndex = 0
        Me.logoBox.TabStop = False
        '
        'Panel6
        '
        Me.Panel6.AutoScroll = True
        Me.Panel6.AutoScrollMinSize = New System.Drawing.Size(1602, 1500)
        Me.Panel6.Controls.Add(Me.pageLabel)
        Me.Panel6.Controls.Add(Me.prevBtn)
        Me.Panel6.Controls.Add(Me.nextBtn)
        Me.Panel6.Controls.Add(Me.userJobLogs)
        Me.Panel6.Controls.Add(Me.userLogs)
        Me.Panel6.Controls.Add(Me.accountType)
        Me.Panel6.Controls.Add(Me.Label5)
        Me.Panel6.Controls.Add(Me.Label1)
        Me.Panel6.Controls.Add(Me.Label2)
        Me.Panel6.Controls.Add(Me.Label3)
        Me.Panel6.Controls.Add(Me.userDepartment)
        Me.Panel6.Controls.Add(Me.Label6)
        Me.Panel6.Controls.Add(Me.userDesig)
        Me.Panel6.Controls.Add(Me.userEmail)
        Me.Panel6.Controls.Add(Me.userMobile)
        Me.Panel6.Controls.Add(Me.userBirthday)
        Me.Panel6.Controls.Add(Me.userName)
        Me.Panel6.Controls.Add(Me.ShapeContainer1)
        Me.Panel6.Dock = System.Windows.Forms.DockStyle.Fill
        Me.Panel6.Location = New System.Drawing.Point(350, 0)
        Me.Panel6.Name = "Panel6"
        Me.Panel6.Size = New System.Drawing.Size(1574, 1061)
        Me.Panel6.TabIndex = 23
        '
        'pageLabel
        '
        Me.pageLabel.AutoSize = True
        Me.pageLabel.BackColor = System.Drawing.Color.White
        Me.pageLabel.Font = New System.Drawing.Font("Courier10 BT", 15.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World)
        Me.pageLabel.Location = New System.Drawing.Point(1292, 580)
        Me.pageLabel.Name = "pageLabel"
        Me.pageLabel.Size = New System.Drawing.Size(107, 17)
        Me.pageLabel.TabIndex = 38
        Me.pageLabel.Text = "Page 1 of 1"
        Me.pageLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        Me.pageLabel.UseMnemonic = False
        '
        'prevBtn
        '
        Me.prevBtn.Font = New System.Drawing.Font("Courier10 BT", 15.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World)
        Me.prevBtn.Location = New System.Drawing.Point(1200, 568)
        Me.prevBtn.Name = "prevBtn"
        Me.prevBtn.Size = New System.Drawing.Size(72, 40)
        Me.prevBtn.TabIndex = 37
        Me.prevBtn.Text = "Prev"
        Me.prevBtn.UseVisualStyleBackColor = True
        '
        'nextBtn
        '
        Me.nextBtn.Font = New System.Drawing.Font("Courier10 BT", 15.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World)
        Me.nextBtn.Location = New System.Drawing.Point(1416, 568)
        Me.nextBtn.Name = "nextBtn"
        Me.nextBtn.Size = New System.Drawing.Size(72, 40)
        Me.nextBtn.TabIndex = 36
        Me.nextBtn.Text = "Next"
        Me.nextBtn.UseVisualStyleBackColor = True
        '
        'userJobLogs
        '
        Me.userJobLogs.BackColor = System.Drawing.Color.White
        Me.userJobLogs.Font = New System.Drawing.Font("Courier10 BT", 30.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World)
        Me.userJobLogs.Location = New System.Drawing.Point(64, 632)
        Me.userJobLogs.Name = "userJobLogs"
        Me.userJobLogs.Size = New System.Drawing.Size(1432, 529)
        Me.userJobLogs.TabIndex = 35
        '
        'userLogs
        '
        Me.userLogs.AutoSize = True
        Me.userLogs.Font = New System.Drawing.Font("Courier10 BT", 23.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.userLogs.Location = New System.Drawing.Point(56, 576)
        Me.userLogs.Margin = New System.Windows.Forms.Padding(0)
        Me.userLogs.Name = "userLogs"
        Me.userLogs.Size = New System.Drawing.Size(169, 37)
        Me.userLogs.TabIndex = 34
        Me.userLogs.Text = "JOB LOGS"
        '
        'accountType
        '
        Me.accountType.AutoSize = True
        Me.accountType.Font = New System.Drawing.Font("Courier10 BT", 30.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World)
        Me.accountType.Location = New System.Drawing.Point(48, 152)
        Me.accountType.Margin = New System.Windows.Forms.Padding(0)
        Me.accountType.Name = "accountType"
        Me.accountType.Size = New System.Drawing.Size(267, 35)
        Me.accountType.TabIndex = 32
        Me.accountType.Text = "(ACCOUNT TYPE)"
        Me.accountType.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        '
        'Label5
        '
        Me.Label5.AutoSize = True
        Me.Label5.Font = New System.Drawing.Font("Courier10 BT", 30.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.Label5.Location = New System.Drawing.Point(784, 360)
        Me.Label5.Margin = New System.Windows.Forms.Padding(0)
        Me.Label5.Name = "Label5"
        Me.Label5.Size = New System.Drawing.Size(213, 35)
        Me.Label5.TabIndex = 26
        Me.Label5.Text = "DEPARTMENT:"
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Font = New System.Drawing.Font("Courier10 BT", 30.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.Label1.Location = New System.Drawing.Point(80, 280)
        Me.Label1.Margin = New System.Windows.Forms.Padding(0)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(123, 35)
        Me.Label1.TabIndex = 22
        Me.Label1.Text = "EMAIL:"
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Font = New System.Drawing.Font("Courier10 BT", 30.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.Label2.Location = New System.Drawing.Point(80, 360)
        Me.Label2.Margin = New System.Windows.Forms.Padding(0)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(177, 35)
        Me.Label2.TabIndex = 23
        Me.Label2.Text = "BIRTHDAY:"
        '
        'Label3
        '
        Me.Label3.AutoSize = True
        Me.Label3.Font = New System.Drawing.Font("Courier10 BT", 30.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.Label3.Location = New System.Drawing.Point(80, 440)
        Me.Label3.Margin = New System.Windows.Forms.Padding(0)
        Me.Label3.Name = "Label3"
        Me.Label3.Size = New System.Drawing.Size(141, 35)
        Me.Label3.TabIndex = 24
        Me.Label3.Text = "MOBILE:"
        '
        'userDepartment
        '
        Me.userDepartment.AutoSize = True
        Me.userDepartment.Font = New System.Drawing.Font("Courier10 BT", 25.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.userDepartment.Location = New System.Drawing.Point(1024, 360)
        Me.userDepartment.Margin = New System.Windows.Forms.Padding(0)
        Me.userDepartment.Name = "userDepartment"
        Me.userDepartment.Size = New System.Drawing.Size(222, 28)
        Me.userDepartment.TabIndex = 31
        Me.userDepartment.Text = "userDepartment"
        '
        'Label6
        '
        Me.Label6.AutoSize = True
        Me.Label6.Font = New System.Drawing.Font("Courier10 BT", 30.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.Label6.Location = New System.Drawing.Point(784, 280)
        Me.Label6.Margin = New System.Windows.Forms.Padding(0)
        Me.Label6.Name = "Label6"
        Me.Label6.Size = New System.Drawing.Size(231, 35)
        Me.Label6.TabIndex = 25
        Me.Label6.Text = "DESIGNATION:"
        '
        'userDesig
        '
        Me.userDesig.AutoSize = True
        Me.userDesig.Font = New System.Drawing.Font("Courier10 BT", 25.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.userDesig.Location = New System.Drawing.Point(1024, 280)
        Me.userDesig.Margin = New System.Windows.Forms.Padding(0)
        Me.userDesig.Name = "userDesig"
        Me.userDesig.Size = New System.Drawing.Size(192, 28)
        Me.userDesig.TabIndex = 30
        Me.userDesig.Text = "userPosition"
        '
        'userEmail
        '
        Me.userEmail.AutoSize = True
        Me.userEmail.Font = New System.Drawing.Font("Courier10 BT", 25.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.userEmail.Location = New System.Drawing.Point(264, 280)
        Me.userEmail.Margin = New System.Windows.Forms.Padding(0)
        Me.userEmail.Name = "userEmail"
        Me.userEmail.Size = New System.Drawing.Size(147, 28)
        Me.userEmail.TabIndex = 27
        Me.userEmail.Text = "userEmail"
        '
        'userMobile
        '
        Me.userMobile.AutoSize = True
        Me.userMobile.Font = New System.Drawing.Font("Courier10 BT", 25.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.userMobile.Location = New System.Drawing.Point(264, 440)
        Me.userMobile.Margin = New System.Windows.Forms.Padding(0)
        Me.userMobile.Name = "userMobile"
        Me.userMobile.Size = New System.Drawing.Size(162, 28)
        Me.userMobile.TabIndex = 29
        Me.userMobile.Text = "userMobile"
        '
        'userBirthday
        '
        Me.userBirthday.AutoSize = True
        Me.userBirthday.Font = New System.Drawing.Font("Courier10 BT", 25.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.userBirthday.Location = New System.Drawing.Point(264, 360)
        Me.userBirthday.Margin = New System.Windows.Forms.Padding(0)
        Me.userBirthday.Name = "userBirthday"
        Me.userBirthday.Size = New System.Drawing.Size(192, 28)
        Me.userBirthday.TabIndex = 28
        Me.userBirthday.Text = "userBirthday"
        '
        'userName
        '
        Me.userName.AutoSize = True
        Me.userName.Font = New System.Drawing.Font("Courier10 BT", 40.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World)
        Me.userName.Location = New System.Drawing.Point(48, 104)
        Me.userName.Margin = New System.Windows.Forms.Padding(0)
        Me.userName.Name = "userName"
        Me.userName.Size = New System.Drawing.Size(212, 46)
        Me.userName.TabIndex = 21
        Me.userName.Text = "USERNAME"
        '
        'ShapeContainer1
        '
        Me.ShapeContainer1.Location = New System.Drawing.Point(0, 0)
        Me.ShapeContainer1.Margin = New System.Windows.Forms.Padding(0)
        Me.ShapeContainer1.Name = "ShapeContainer1"
        Me.ShapeContainer1.Shapes.AddRange(New Microsoft.VisualBasic.PowerPacks.Shape() {Me.LineShape2, Me.LineShape1})
        Me.ShapeContainer1.Size = New System.Drawing.Size(1602, 1500)
        Me.ShapeContainer1.TabIndex = 33
        Me.ShapeContainer1.TabStop = False
        '
        'LineShape2
        '
        Me.LineShape2.AccessibleRole = System.Windows.Forms.AccessibleRole.Separator
        Me.LineShape2.Enabled = False
        Me.LineShape2.Name = "LineShape2"
        Me.LineShape2.X1 = 52
        Me.LineShape2.X2 = 1489
        Me.LineShape2.Y1 = 228
        Me.LineShape2.Y2 = 228
        '
        'LineShape1
        '
        Me.LineShape1.AccessibleRole = System.Windows.Forms.AccessibleRole.Separator
        Me.LineShape1.Enabled = False
        Me.LineShape1.Name = "LineShape1"
        Me.LineShape1.X1 = 52
        Me.LineShape1.X2 = 1489
        Me.LineShape1.Y1 = 526
        Me.LineShape1.Y2 = 526
        '
        'landingPageSignatory
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1924, 1061)
        Me.Controls.Add(Me.Panel6)
        Me.Controls.Add(Me.Panel1)
        Me.Name = "landingPageSignatory"
        Me.Text = "Form1"
        Me.Panel1.ResumeLayout(False)
        Me.Panel8.ResumeLayout(False)
        Me.Panel4.ResumeLayout(False)
        Me.Panel2.ResumeLayout(False)
        CType(Me.logoBox, System.ComponentModel.ISupportInitialize).EndInit()
        Me.Panel6.ResumeLayout(False)
        Me.Panel6.PerformLayout()
        Me.ResumeLayout(False)

    End Sub
    Friend WithEvents Panel1 As System.Windows.Forms.Panel
    Friend WithEvents Panel8 As System.Windows.Forms.Panel
    Friend WithEvents logoutBtn As System.Windows.Forms.Button
    Friend WithEvents Panel4 As System.Windows.Forms.Panel
    Friend WithEvents Button2 As System.Windows.Forms.Button
    Friend WithEvents Panel2 As System.Windows.Forms.Panel
    Friend WithEvents logoBox As System.Windows.Forms.PictureBox
    Friend WithEvents Panel6 As System.Windows.Forms.Panel
    Friend WithEvents accountType As System.Windows.Forms.Label
    Friend WithEvents Label5 As System.Windows.Forms.Label
    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents Label2 As System.Windows.Forms.Label
    Friend WithEvents Label3 As System.Windows.Forms.Label
    Friend WithEvents userDepartment As System.Windows.Forms.Label
    Friend WithEvents Label6 As System.Windows.Forms.Label
    Friend WithEvents userDesig As System.Windows.Forms.Label
    Friend WithEvents userEmail As System.Windows.Forms.Label
    Friend WithEvents userMobile As System.Windows.Forms.Label
    Friend WithEvents userBirthday As System.Windows.Forms.Label
    Friend WithEvents userName As System.Windows.Forms.Label
    Friend WithEvents ShapeContainer1 As Microsoft.VisualBasic.PowerPacks.ShapeContainer
    Friend WithEvents LineShape2 As Microsoft.VisualBasic.PowerPacks.LineShape
    Friend WithEvents LineShape1 As Microsoft.VisualBasic.PowerPacks.LineShape
    Friend WithEvents userJobLogs As System.Windows.Forms.FlowLayoutPanel
    Friend WithEvents userLogs As System.Windows.Forms.Label
    Friend WithEvents pageLabel As System.Windows.Forms.Label
    Friend WithEvents prevBtn As System.Windows.Forms.Button
    Friend WithEvents nextBtn As System.Windows.Forms.Button
End Class
