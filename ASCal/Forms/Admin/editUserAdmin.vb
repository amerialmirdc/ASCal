Imports System.Windows.Forms
Imports ASCal.userManagementAdmin
Imports ASCal.SQLiteHelper
Imports ASCal.SessionManager
Imports ASCal.UIHelper
Imports System.Data.SQLite


Public Class editUserAdmin

    Private originalInitials As String
    Private originalName As String
    Private originalAccountType As String

    ' ===== Unified Button Click Handler =====
    Private Sub HandleNavClick(sender As Object, e As EventArgs) Handles PictureBox1.Click, Button4.Click, compMan.Click, Button3.Click, logoutBtn.Click, Button1.Click

        calibrate.RefreshData()
        Me.Close()

        Select Case True
            Case sender Is PictureBox1
                landingPageAdmin.Show()
            Case sender Is Button4
                MessageBox.Show("JOB MANAGEMENT")
            Case sender Is compMan
                compManagementAdmin.Show()
            Case sender Is Button3
                userManagementAdmin.Show()
            Case sender Is logoutBtn
                login.Show()
            Case sender Is Button1
                dmmManagementAdmin.Show()
        End Select
    End Sub

    '' ===== SUB editUserForm_Load =====
    Private Sub editUserForm_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load

        ' Make sure start position is manual
        Me.StartPosition = FormStartPosition.Manual

        ' Remove designer overrides
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)

        ' Get working area excluding the taskbar
        Dim currentScreen As Screen = Screen.FromControl(Me)
        Dim workingArea As Rectangle = currentScreen.WorkingArea

        ' Apply correct size and location
        Me.Bounds = Screen.FromControl(Me).WorkingArea


        originalName = currentPerson.Name
        originalAccountType = currentPerson.AccountType


        ForceUppercaseInput(Me)

    End Sub

    ' ✅ Ito ang object ng user na ie-edit
    Private currentPerson As Personnel
    Private jobs As Job

    ' ✅ Constructor na tatanggap ng Personnel object na ie-edit
    '' ===== SUB New =====
    '' Purpose: [Describe what New does]
    Public Sub New(person As Personnel)
        InitializeComponent()
        currentPerson = person
        LoadUserData() ' Tawagin agad to populate form fields
    End Sub

    ' ✅ Load user details sa mga textboxes
    '' ===== SUB LoadUserData =====
    '' Purpose: [Describe what LoadUserData does]
    Private Sub LoadUserData()
        usernameUser.Text = currentPerson.Username
        nameUser.Text = currentPerson.Name
        birthdayUser.Text = currentPerson.Birthday
        emailUser.Text = currentPerson.Email
        contNumUser.Text = currentPerson.ContactNumber
        desigUser.Text = currentPerson.Designation
        deptUser.Text = currentPerson.Department


        ' Add combo box items para sa Account Type (isa lang ilalagay)
        accntTypUser.Items.Clear()
        accntTypUser.Items.AddRange(New String() {"Administrator", "Signatory", "Technician"})
        accntTypUser.DropDownStyle = ComboBoxStyle.DropDownList
        accntTypUser.Text = currentPerson.AccountType

        If accntTypUser.Text = "Signatory" Then
            sigType.Visible = True
            Label3.Visible = True
            signUser.Visible = True
            Label12.Visible = True
            newBrowseBtn.Visible = True
            sigType.Items.Clear()
            sigType.Items.AddRange(New String() {"Initial", "Quality Assurance"})
            accntTypUser.DropDownStyle = ComboBoxStyle.DropDownList
            sigType.Text = currentPerson.SignatoryType
        Else
            sigType.Visible = False
            Label3.Visible = False
            signUser.Visible = False
            Label12.Visible = False
            newBrowseBtn.Visible = False
        End If

        ' Clear password fields
        oldPassUser.Text = ""
        newPassUser.Text = ""
        confNewPassUser.Text = ""

        ' Disable muna ang bagong password fields hangga’t di naglalagay ng old password
        newPassUser.Enabled = False
        confNewPassUser.Enabled = False
    End Sub

    ' ✅ Enable or disable new password fields kapag may nilagay na old password
    '' ===== SUB oldPassUser_TextChanged =====
    '' Purpose: [Describe what oldPassUser_TextChanged does]
    Private Sub oldPassUser_TextChanged(sender As Object, e As EventArgs) Handles oldPassUser.TextChanged
        Dim hasOldPass As Boolean = Not String.IsNullOrWhiteSpace(oldPassUser.Text.Trim())
        newPassUser.Enabled = hasOldPass
        confNewPassUser.Enabled = hasOldPass
    End Sub

    ' ✅ Back button (Close lang ang form)
    '' ===== SUB backBtn_Click =====
    '' Purpose: [Describe what backBtn_Click does]
    Private Sub backBtn_Click(sender As Object, e As EventArgs) Handles backBtn.Click
        Me.Close()
    End Sub

    ' ✅ Save button na may validation bago i-save
    '' ===== SUB saveBtn_Click =====
    '' Purpose: [Describe what saveBtn_Click does]
    Private Sub saveBtn_Click(sender As Object, e As EventArgs) Handles saveBtn.Click
        Dim oldPasswordInput As String = oldPassUser.Text.Trim()
        Dim newPasswordInput As String = newPassUser.Text.Trim()
        Dim confirmPasswordInput As String = confNewPassUser.Text.Trim()
        Dim newUsername As String = usernameUser.Text.Trim()


        Dim oldInitials = currentPerson.Initials
        Dim oldname = currentPerson.Name
        Dim accountType = currentPerson.AccountType
        Dim newName As String = nameUser.Text.Trim()
        Dim newInitials As String = GetInitials(newName)

        ' Assign to currentPerson before calling update
        currentPerson.Name = newName
        currentPerson.Initials = newInitials

        SQLiteHelper.UpdateJobRecordsIfUserChanged(newInitials, oldInitials, originalName, newName, accountType)



        ' Check kung may existing na username maliban sa kasalukuyang user
        Dim usernameExists As Boolean = userManagementAdmin.personnelList.Any(Function(p) p.Username.ToLower() = newUsername.ToLower() AndAlso p.ID <> currentPerson.ID)
        If usernameExists Then
            MessageBox.Show("Username already exists. Please choose a different username.", "Username Conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            usernameUser.Focus()
            Exit Sub
        End If

        ' ✅ Kung magpapalit ng password
        If Not String.IsNullOrWhiteSpace(oldPasswordInput) Then
            ' Check kung tama ang old password
            If oldPasswordInput <> currentPerson.Password Then
                MessageBox.Show("Old password does not match.", "Password Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                oldPassUser.Focus()
                Exit Sub
            End If

            ' Check kung may nilagay na new password at confirmation
            If String.IsNullOrWhiteSpace(newPasswordInput) OrElse String.IsNullOrWhiteSpace(confirmPasswordInput) Then
                MessageBox.Show("Please input both new password and confirmation.", "Missing Fields", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                newPassUser.Focus()
                Exit Sub
            End If

            ' Check kung nagmamatch ang new password at confirm password
            If newPasswordInput <> confirmPasswordInput Then
                MessageBox.Show("New password and confirmation do not match.", "Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Error)
                newPassUser.Focus()
                Exit Sub
            End If

            ' Update password field
            currentPerson.Password = newPasswordInput
        End If

        ' ✅ Update lahat ng fields
        currentPerson.Username = newUsername
        currentPerson.Name = nameUser.Text.Trim()
        currentPerson.Birthday = birthdayUser.Text.Trim()
        currentPerson.Email = emailUser.Text.Trim()
        currentPerson.ContactNumber = contNumUser.Text.Trim()
        currentPerson.Designation = desigUser.Text.Trim()
        currentPerson.Department = deptUser.Text.Trim()
        currentPerson.AccountType = accntTypUser.Text.Trim()
        currentPerson.SignatoryType = sigType.Text.Trim()


        ' ✅ Save to database
        SaveUser(currentPerson)
        MessageBox.Show("User details updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Me.Close()
    End Sub



    ' ✅ Show/Hide toggle para sa old password
    '' ===== SUB showOldPassBtn_Click =====
    '' Purpose: [Describe what showOldPassBtn_Click does]
    Private Sub showOldPassBtn_Click(sender As Object, e As EventArgs) Handles showOldPassBtn.Click
        oldPassUser.UseSystemPasswordChar = Not oldPassUser.UseSystemPasswordChar
        showOldPassBtn.Text = If(oldPassUser.UseSystemPasswordChar, "👁", "🙈")
    End Sub

    ' ✅ Show/Hide toggle para sa new password field
    '' ===== SUB showNewPassBtn_Click =====
    '' Purpose: [Describe what showNewPassBtn_Click does]
    Private Sub showNewPassBtn_Click(sender As Object, e As EventArgs) Handles showNewPassBtn.Click
        newPassUser.UseSystemPasswordChar = Not newPassUser.UseSystemPasswordChar
        showNewPassBtn.Text = If(newPassUser.UseSystemPasswordChar, "👁", "🙈")
    End Sub

    ' ✅ Show/Hide toggle para sa confirm new password field
    '' ===== SUB showConfPassBtn_Click =====
    '' Purpose: [Describe what showConfPassBtn_Click does]
    Private Sub showConfPassBtn_Click(sender As Object, e As EventArgs) Handles showConfPassBtn.Click
        confNewPassUser.UseSystemPasswordChar = Not confNewPassUser.UseSystemPasswordChar
        showConfPassBtn.Text = If(confNewPassUser.UseSystemPasswordChar, "👁", "🙈")
    End Sub

    ' ✅ Delete button logic with password confirmation of logged-in user
    '' ===== SUB dltBtn_Click =====
    '' Purpose: [Describe what dltBtn_Click does]
    Private Sub dltBtn_Click(sender As Object, e As EventArgs) Handles dltBtn.Click
        ' Mag-prompt na hingin ang password ng kasalukuyang logged-in user
        Dim passwordInput As String = InputBox("Please enter your password to confirm deletion of this user:", "Confirm Delete")

        If String.IsNullOrEmpty(passwordInput) Then
            MessageBox.Show("Deletion cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Exit Sub
        End If

        ' Check kung tama ang password ng logged-in user
        If passwordInput <> SessionManager.LoggedInUser.Password Then
            MessageBox.Show("Incorrect password. Deletion aborted.", "Password Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If

        ' Confirm final deletion
        Dim confirmResult As DialogResult = MessageBox.Show("Are you sure you want to permanently delete this user?", "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
        If confirmResult = DialogResult.Yes Then
            DeleteUser(currentPerson.ID)
            MessageBox.Show("User deleted successfully.", "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information)

            ' Refresh grid kung bukas ang userManagementAdmin
            If Application.OpenForms().OfType(Of userManagementAdmin).Any() Then
                Dim form As userManagementAdmin = Application.OpenForms().OfType(Of userManagementAdmin).First()
                form.RefreshGrid()
            End If

            Me.Close()
        End If
    End Sub

    ' ✅ Ipakita agad dropdown kapag clinic-click ang combobox
    '' ===== SUB accntTypUser_MouseClick =====
    '' Purpose: [Describe what accntTypUser_MouseClick does]
    Private Sub accntTypUser_MouseClick(sender As Object, e As MouseEventArgs) Handles accntTypUser.MouseClick
        accntTypUser.DroppedDown = True
    End Sub

    ' ✅ I-prevent ang manual typing — dropdown only
    '' ===== SUB accntTypUser_KeyPress =====
    '' Purpose: [Describe what accntTypUser_KeyPress does]
    Private Sub accntTypUser_KeyPress(sender As Object, e As KeyPressEventArgs) Handles accntTypUser.KeyPress
        e.Handled = True
    End Sub

    Private Shared Function GetInitials(fullName As String) As String
        Dim initials As String = ""
        Dim parts() As String = fullName.Trim().Split(" "c)
        For Each part As String In parts
            If Not String.IsNullOrWhiteSpace(part) Then
                initials &= Char.ToUpper(part(0))
            End If
        Next
        Return initials
    End Function


End Class
