<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class landingPageAdmin
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
        Me.userName = New System.Windows.Forms.Label()
        Me.LineShape2 = New Microsoft.VisualBasic.PowerPacks.LineShape()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.Label3 = New System.Windows.Forms.Label()
        Me.Label5 = New System.Windows.Forms.Label()
        Me.Label6 = New System.Windows.Forms.Label()
        Me.userEmail = New System.Windows.Forms.Label()
        Me.userBirthday = New System.Windows.Forms.Label()
        Me.userMobile = New System.Windows.Forms.Label()
        Me.userDepartment = New System.Windows.Forms.Label()
        Me.userDesig = New System.Windows.Forms.Label()
        Me.Panel1 = New System.Windows.Forms.Panel()
        Me.Panel6 = New System.Windows.Forms.Panel()
        Me.Button1 = New System.Windows.Forms.Button()
        Me.Panel3 = New System.Windows.Forms.Panel()
        Me.compMan = New System.Windows.Forms.Button()
        Me.Panel8 = New System.Windows.Forms.Panel()
        Me.logoutBtn = New System.Windows.Forms.Button()
        Me.Panel5 = New System.Windows.Forms.Panel()
        Me.userManagementBtn = New System.Windows.Forms.Button()
        Me.Panel4 = New System.Windows.Forms.Panel()
        Me.Button2 = New System.Windows.Forms.Button()
        Me.Panel2 = New System.Windows.Forms.Panel()
        Me.logoBox = New System.Windows.Forms.PictureBox()
        Me.Panel7 = New System.Windows.Forms.Panel()
        Me.accountType = New System.Windows.Forms.Label()
        Me.ShapeContainer2 = New Microsoft.VisualBasic.PowerPacks.ShapeContainer()
        Me.LineShape1 = New Microsoft.VisualBasic.PowerPacks.LineShape()
        Me.Panel1.SuspendLayout()
        Me.Panel6.SuspendLayout()
        Me.Panel3.SuspendLayout()
        Me.Panel8.SuspendLayout()
        Me.Panel5.SuspendLayout()
        Me.Panel4.SuspendLayout()
        Me.Panel2.SuspendLayout()
        CType(Me.logoBox, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.Panel7.SuspendLayout()
        Me.SuspendLayout()
        '
        'userName
        '
        Me.userName.AutoSize = True
        Me.userName.Font = New System.Drawing.Font("Courier10 BT", 40.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World)
        Me.userName.Location = New System.Drawing.Point(24, 104)
        Me.userName.Margin = New System.Windows.Forms.Padding(0)
        Me.userName.Name = "userName"
        Me.userName.Size = New System.Drawing.Size(212, 46)
        Me.userName.TabIndex = 2
        Me.userName.Text = "USERNAME"
        '
        'LineShape2
        '
        Me.LineShape2.AccessibleRole = System.Windows.Forms.AccessibleRole.Separator
        Me.LineShape2.Enabled = False
        Me.LineShape2.Name = "LineShape2"
        Me.LineShape2.X1 = 28
        Me.LineShape2.X2 = 1465
        Me.LineShape2.Y1 = 224
        Me.LineShape2.Y2 = 224
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Font = New System.Drawing.Font("Courier10 BT", 30.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.Label1.Location = New System.Drawing.Point(24, 280)
        Me.Label1.Margin = New System.Windows.Forms.Padding(0)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(123, 35)
        Me.Label1.TabIndex = 5
        Me.Label1.Text = "EMAIL:"
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Font = New System.Drawing.Font("Courier10 BT", 30.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.Label2.Location = New System.Drawing.Point(24, 360)
        Me.Label2.Margin = New System.Windows.Forms.Padding(0)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(177, 35)
        Me.Label2.TabIndex = 6
        Me.Label2.Text = "BIRTHDAY:"
        '
        'Label3
        '
        Me.Label3.AutoSize = True
        Me.Label3.Font = New System.Drawing.Font("Courier10 BT", 30.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.Label3.Location = New System.Drawing.Point(24, 440)
        Me.Label3.Margin = New System.Windows.Forms.Padding(0)
        Me.Label3.Name = "Label3"
        Me.Label3.Size = New System.Drawing.Size(141, 35)
        Me.Label3.TabIndex = 7
        Me.Label3.Text = "MOBILE:"
        '
        'Label5
        '
        Me.Label5.AutoSize = True
        Me.Label5.Font = New System.Drawing.Font("Courier10 BT", 30.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.Label5.Location = New System.Drawing.Point(728, 360)
        Me.Label5.Margin = New System.Windows.Forms.Padding(0)
        Me.Label5.Name = "Label5"
        Me.Label5.Size = New System.Drawing.Size(213, 35)
        Me.Label5.TabIndex = 9
        Me.Label5.Text = "DEPARTMENT:"
        '
        'Label6
        '
        Me.Label6.AutoSize = True
        Me.Label6.Font = New System.Drawing.Font("Courier10 BT", 30.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.Label6.Location = New System.Drawing.Point(728, 280)
        Me.Label6.Margin = New System.Windows.Forms.Padding(0)
        Me.Label6.Name = "Label6"
        Me.Label6.Size = New System.Drawing.Size(231, 35)
        Me.Label6.TabIndex = 8
        Me.Label6.Text = "DESIGNATION:"
        '
        'userEmail
        '
        Me.userEmail.AutoSize = True
        Me.userEmail.Font = New System.Drawing.Font("Courier10 BT", 25.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.userEmail.Location = New System.Drawing.Point(208, 280)
        Me.userEmail.Margin = New System.Windows.Forms.Padding(0)
        Me.userEmail.Name = "userEmail"
        Me.userEmail.Size = New System.Drawing.Size(147, 28)
        Me.userEmail.TabIndex = 11
        Me.userEmail.Text = "userEmail"
        '
        'userBirthday
        '
        Me.userBirthday.AutoSize = True
        Me.userBirthday.Font = New System.Drawing.Font("Courier10 BT", 25.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.userBirthday.Location = New System.Drawing.Point(208, 360)
        Me.userBirthday.Margin = New System.Windows.Forms.Padding(0)
        Me.userBirthday.Name = "userBirthday"
        Me.userBirthday.Size = New System.Drawing.Size(192, 28)
        Me.userBirthday.TabIndex = 12
        Me.userBirthday.Text = "userBirthday"
        '
        'userMobile
        '
        Me.userMobile.AutoSize = True
        Me.userMobile.Font = New System.Drawing.Font("Courier10 BT", 25.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.userMobile.Location = New System.Drawing.Point(208, 440)
        Me.userMobile.Margin = New System.Windows.Forms.Padding(0)
        Me.userMobile.Name = "userMobile"
        Me.userMobile.Size = New System.Drawing.Size(162, 28)
        Me.userMobile.TabIndex = 13
        Me.userMobile.Text = "userMobile"
        '
        'userDepartment
        '
        Me.userDepartment.AutoSize = True
        Me.userDepartment.Font = New System.Drawing.Font("Courier10 BT", 25.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.userDepartment.Location = New System.Drawing.Point(968, 360)
        Me.userDepartment.Margin = New System.Windows.Forms.Padding(0)
        Me.userDepartment.Name = "userDepartment"
        Me.userDepartment.Size = New System.Drawing.Size(222, 28)
        Me.userDepartment.TabIndex = 15
        Me.userDepartment.Text = "userDepartment"
        '
        'userDesig
        '
        Me.userDesig.AutoSize = True
        Me.userDesig.Font = New System.Drawing.Font("Courier10 BT", 25.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World, CType(0, Byte))
        Me.userDesig.Location = New System.Drawing.Point(968, 280)
        Me.userDesig.Margin = New System.Windows.Forms.Padding(0)
        Me.userDesig.Name = "userDesig"
        Me.userDesig.Size = New System.Drawing.Size(192, 28)
        Me.userDesig.TabIndex = 14
        Me.userDesig.Text = "userPosition"
        '
        'Panel1
        '
        Me.Panel1.AutoScroll = True
        Me.Panel1.BackColor = System.Drawing.Color.Cyan
        Me.Panel1.Controls.Add(Me.Panel6)
        Me.Panel1.Controls.Add(Me.Panel3)
        Me.Panel1.Controls.Add(Me.Panel8)
        Me.Panel1.Controls.Add(Me.Panel5)
        Me.Panel1.Controls.Add(Me.Panel4)
        Me.Panel1.Controls.Add(Me.Panel2)
        Me.Panel1.Dock = System.Windows.Forms.DockStyle.Left
        Me.Panel1.Location = New System.Drawing.Point(0, 0)
        Me.Panel1.Name = "Panel1"
        Me.Panel1.Size = New System.Drawing.Size(300, 1061)
        Me.Panel1.TabIndex = 21
        '
        'Panel6
        '
        Me.Panel6.Controls.Add(Me.Button1)
        Me.Panel6.Dock = System.Windows.Forms.DockStyle.Top
        Me.Panel6.Location = New System.Drawing.Point(0, 304)
        Me.Panel6.Name = "Panel6"
        Me.Panel6.Size = New System.Drawing.Size(300, 68)
        Me.Panel6.TabIndex = 13
        '
        'Button1
        '
        Me.Button1.BackColor = System.Drawing.Color.Cyan
        Me.Button1.Cursor = System.Windows.Forms.Cursors.Hand
        Me.Button1.FlatAppearance.BorderColor = System.Drawing.Color.Black
        Me.Button1.FlatAppearance.MouseDownBackColor = System.Drawing.Color.White
        Me.Button1.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Aqua
        Me.Button1.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.Button1.Font = New System.Drawing.Font("Courier10 BT", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Button1.Location = New System.Drawing.Point(32, 16)
        Me.Button1.Name = "Button1"
        Me.Button1.Size = New System.Drawing.Size(245, 41)
        Me.Button1.TabIndex = 0
        Me.Button1.Text = "DMM MANAGEMENT"
        Me.Button1.UseVisualStyleBackColor = False
        '
        'Panel3
        '
        Me.Panel3.Controls.Add(Me.compMan)
        Me.Panel3.Dock = System.Windows.Forms.DockStyle.Top
        Me.Panel3.Location = New System.Drawing.Point(0, 236)
        Me.Panel3.Name = "Panel3"
        Me.Panel3.Size = New System.Drawing.Size(300, 68)
        Me.Panel3.TabIndex = 7
        '
        'compMan
        '
        Me.compMan.BackColor = System.Drawing.Color.Cyan
        Me.compMan.FlatAppearance.BorderColor = System.Drawing.Color.Black
        Me.compMan.FlatAppearance.MouseDownBackColor = System.Drawing.Color.White
        Me.compMan.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Aqua
        Me.compMan.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.compMan.Font = New System.Drawing.Font("Courier10 BT", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.compMan.Location = New System.Drawing.Point(32, 16)
        Me.compMan.Name = "compMan"
        Me.compMan.Size = New System.Drawing.Size(245, 41)
        Me.compMan.TabIndex = 0
        Me.compMan.Text = "COMPANY MANAGEMENT"
        Me.compMan.UseVisualStyleBackColor = False
        '
        'Panel8
        '
        Me.Panel8.Controls.Add(Me.logoutBtn)
        Me.Panel8.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.Panel8.Location = New System.Drawing.Point(0, 993)
        Me.Panel8.Name = "Panel8"
        Me.Panel8.Size = New System.Drawing.Size(300, 68)
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
        'Panel5
        '
        Me.Panel5.Controls.Add(Me.userManagementBtn)
        Me.Panel5.Dock = System.Windows.Forms.DockStyle.Top
        Me.Panel5.Location = New System.Drawing.Point(0, 168)
        Me.Panel5.Name = "Panel5"
        Me.Panel5.Size = New System.Drawing.Size(300, 68)
        Me.Panel5.TabIndex = 3
        '
        'userManagementBtn
        '
        Me.userManagementBtn.BackColor = System.Drawing.Color.Cyan
        Me.userManagementBtn.FlatAppearance.BorderColor = System.Drawing.Color.Black
        Me.userManagementBtn.FlatAppearance.MouseDownBackColor = System.Drawing.Color.White
        Me.userManagementBtn.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Aqua
        Me.userManagementBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.userManagementBtn.Font = New System.Drawing.Font("Courier10 BT", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.userManagementBtn.Location = New System.Drawing.Point(32, 16)
        Me.userManagementBtn.Name = "userManagementBtn"
        Me.userManagementBtn.Size = New System.Drawing.Size(245, 41)
        Me.userManagementBtn.TabIndex = 0
        Me.userManagementBtn.Text = "USER MANAGEMENT"
        Me.userManagementBtn.UseVisualStyleBackColor = False
        '
        'Panel4
        '
        Me.Panel4.Controls.Add(Me.Button2)
        Me.Panel4.Dock = System.Windows.Forms.DockStyle.Top
        Me.Panel4.Location = New System.Drawing.Point(0, 100)
        Me.Panel4.Name = "Panel4"
        Me.Panel4.Size = New System.Drawing.Size(300, 68)
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
        Me.Button2.Size = New System.Drawing.Size(245, 41)
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
        Me.Panel2.Size = New System.Drawing.Size(300, 100)
        Me.Panel2.TabIndex = 0
        '
        'logoBox
        '
        Me.logoBox.Image = Global.ASCal.My.Resources.Resources._31
        Me.logoBox.Location = New System.Drawing.Point(56, 24)
        Me.logoBox.Name = "logoBox"
        Me.logoBox.Size = New System.Drawing.Size(192, 66)
        Me.logoBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage
        Me.logoBox.TabIndex = 0
        Me.logoBox.TabStop = False
        '
        'Panel7
        '
        Me.Panel7.AutoScroll = True
        Me.Panel7.Controls.Add(Me.accountType)
        Me.Panel7.Controls.Add(Me.Label5)
        Me.Panel7.Controls.Add(Me.Label1)
        Me.Panel7.Controls.Add(Me.Label2)
        Me.Panel7.Controls.Add(Me.Label3)
        Me.Panel7.Controls.Add(Me.userDepartment)
        Me.Panel7.Controls.Add(Me.Label6)
        Me.Panel7.Controls.Add(Me.userDesig)
        Me.Panel7.Controls.Add(Me.userEmail)
        Me.Panel7.Controls.Add(Me.userMobile)
        Me.Panel7.Controls.Add(Me.userBirthday)
        Me.Panel7.Controls.Add(Me.userName)
        Me.Panel7.Controls.Add(Me.ShapeContainer2)
        Me.Panel7.Dock = System.Windows.Forms.DockStyle.Fill
        Me.Panel7.Location = New System.Drawing.Point(300, 0)
        Me.Panel7.Name = "Panel7"
        Me.Panel7.Size = New System.Drawing.Size(1624, 1061)
        Me.Panel7.TabIndex = 22
        '
        'accountType
        '
        Me.accountType.AutoSize = True
        Me.accountType.Font = New System.Drawing.Font("Courier10 BT", 30.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.World)
        Me.accountType.Location = New System.Drawing.Point(24, 152)
        Me.accountType.Margin = New System.Windows.Forms.Padding(0)
        Me.accountType.Name = "accountType"
        Me.accountType.Size = New System.Drawing.Size(267, 35)
        Me.accountType.TabIndex = 20
        Me.accountType.Text = "(ACCOUNT TYPE)"
        Me.accountType.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        '
        'ShapeContainer2
        '
        Me.ShapeContainer2.Location = New System.Drawing.Point(0, 0)
        Me.ShapeContainer2.Margin = New System.Windows.Forms.Padding(0)
        Me.ShapeContainer2.Name = "ShapeContainer2"
        Me.ShapeContainer2.Shapes.AddRange(New Microsoft.VisualBasic.PowerPacks.Shape() {Me.LineShape1, Me.LineShape2})
        Me.ShapeContainer2.Size = New System.Drawing.Size(1624, 1061)
        Me.ShapeContainer2.TabIndex = 3
        Me.ShapeContainer2.TabStop = False
        '
        'LineShape1
        '
        Me.LineShape1.AccessibleRole = System.Windows.Forms.AccessibleRole.Separator
        Me.LineShape1.Enabled = False
        Me.LineShape1.Name = "LineShape1"
        Me.LineShape1.X1 = 28
        Me.LineShape1.X2 = 1465
        Me.LineShape1.Y1 = 522
        Me.LineShape1.Y2 = 522
        '
        'landingPageAdmin
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.AutoSize = True
        Me.AutoValidate = System.Windows.Forms.AutoValidate.EnablePreventFocusChange
        Me.ClientSize = New System.Drawing.Size(1924, 1061)
        Me.Controls.Add(Me.Panel7)
        Me.Controls.Add(Me.Panel1)
        Me.Name = "landingPageAdmin"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "ASCAL"
        Me.Panel1.ResumeLayout(False)
        Me.Panel6.ResumeLayout(False)
        Me.Panel3.ResumeLayout(False)
        Me.Panel8.ResumeLayout(False)
        Me.Panel5.ResumeLayout(False)
        Me.Panel4.ResumeLayout(False)
        Me.Panel2.ResumeLayout(False)
        CType(Me.logoBox, System.ComponentModel.ISupportInitialize).EndInit()
        Me.Panel7.ResumeLayout(False)
        Me.Panel7.PerformLayout()
        Me.ResumeLayout(False)

    End Sub
    Friend WithEvents userName As System.Windows.Forms.Label
    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents Label2 As System.Windows.Forms.Label
    Friend WithEvents Label3 As System.Windows.Forms.Label
    Friend WithEvents Label5 As System.Windows.Forms.Label
    Friend WithEvents Label6 As System.Windows.Forms.Label
    Friend WithEvents userEmail As System.Windows.Forms.Label
    Friend WithEvents userBirthday As System.Windows.Forms.Label
    Friend WithEvents userMobile As System.Windows.Forms.Label
    Friend WithEvents userDepartment As System.Windows.Forms.Label
    Friend WithEvents userDesig As System.Windows.Forms.Label
    Friend WithEvents LineShape2 As Microsoft.VisualBasic.PowerPacks.LineShape
    Friend WithEvents Panel1 As System.Windows.Forms.Panel
    Friend WithEvents Panel5 As System.Windows.Forms.Panel
    Friend WithEvents userManagementBtn As System.Windows.Forms.Button
    Friend WithEvents Panel4 As System.Windows.Forms.Panel
    Friend WithEvents Button2 As System.Windows.Forms.Button
    Friend WithEvents Panel2 As System.Windows.Forms.Panel
    Friend WithEvents logoBox As System.Windows.Forms.PictureBox
    Friend WithEvents Panel7 As System.Windows.Forms.Panel
    Friend WithEvents ShapeContainer2 As Microsoft.VisualBasic.PowerPacks.ShapeContainer
    Friend WithEvents accountType As System.Windows.Forms.Label
    Friend WithEvents Panel8 As System.Windows.Forms.Panel
    Friend WithEvents logoutBtn As System.Windows.Forms.Button
    Friend WithEvents LineShape1 As Microsoft.VisualBasic.PowerPacks.LineShape
    Friend WithEvents Panel3 As System.Windows.Forms.Panel
    Friend WithEvents compMan As System.Windows.Forms.Button
    Friend WithEvents Panel6 As System.Windows.Forms.Panel
    Friend WithEvents Button1 As System.Windows.Forms.Button
End Class
