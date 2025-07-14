Imports ASCal.SQLiteHelper
Imports ASCal.compManagementAdmin
Imports ASCal.SessionManager
Imports System.Windows.Forms
Imports System.Data.SQLite


Public Class editCompanyAdmin

    ' ✅ Unified navigation handler
    Private Sub HandleNavbarClick(sender As Object, e As EventArgs) Handles PictureBox1.Click, Button2.Click, userManagementBtn.Click, compMan.Click, logoutBtn.Click, Button1.Click

        calibrate.RefreshData()

        Select Case True
            Case sender Is PictureBox1
                landingPageAdmin.Show()
                Me.Close()
            Case sender Is Button2
                jobDashAdmin.Show()
                Me.Close()
            Case sender Is userManagementBtn
                userManagementAdmin.Show()
                Me.Close()
            Case sender Is compMan
                compManagementAdmin.Show()
                Me.Close()
            Case sender Is logoutBtn
                login.Show()
            Case sender Is Button1
                dmmManagementAdmin.Show()
                Me.Close()
        End Select
    End Sub



    Private currentComp As Company

    Friend Sub New(comp As Company)
        InitializeComponent()
        currentComp = comp
        LoadCompanyData()
    End Sub

    Private Sub editCompanyForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        ' Make sure start position is manual
        Me.StartPosition = FormStartPosition.Manual

        ' Remove designer overrides
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)

        ' Get working area excluding the taskbar
        Dim currentScreen As Screen = Screen.FromControl(Me)
        Dim workingArea As Rectangle = currentScreen.WorkingArea

    End Sub

    Private Sub LoadCompanyData()
        nameComp.Text = currentComp.Name
        addComp.Text = currentComp.Address
        emailComp.Text = currentComp.Email
        contNameComp.Text = currentComp.ContactPerson
        contNumComp.Text = currentComp.ContactNumber
        desigComp.Text = currentComp.Status
    End Sub

    Private Sub saveBtn_Click(sender As Object, e As EventArgs) Handles saveBtn.Click
        Dim name As String = nameComp.Text.Trim()
        Dim address As String = addComp.Text.Trim()
        Dim email As String = emailComp.Text.Trim()
        Dim contactPerson As String = contNameComp.Text.Trim()
        Dim contactNumber As String = contNumComp.Text.Trim()
        Dim status As String = desigComp.Text.Trim()

        ' Basic validation
        If name = "" Or address = "" Or email = "" Or contactPerson = "" Or contactNumber = "" Then
            MessageBox.Show("Please complete all required fields.", "Missing Info", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' Optional: validate email format
        If Not System.Text.RegularExpressions.Regex.IsMatch(email, "^[^@\s]+@[^@\s]+\.[^@\s]+$") Then
            MessageBox.Show("Please enter a valid email address.", "Invalid Email", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' Update current company object
        currentComp.Name = name
        currentComp.Address = address
        currentComp.Email = email
        currentComp.ContactPerson = contactPerson
        currentComp.ContactNumber = contactNumber
        currentComp.Status = status
        currentComp.DateEnrolled = Date.Now.ToString("yyyy-MM-dd") ' 🕓 Optional update

        Try
            UpdateCompany(currentComp) ' <-- now handled in SQLiteHelper.vb
            MessageBox.Show("Company details updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Me.Close()
            compManagementAdmin.Show()
        Catch ex As Exception
            MessageBox.Show("Error updating company: " & ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try

    End Sub

    Private Sub dltBtn_Click(sender As Object, e As EventArgs) Handles dltBtn.Click
        Dim passwordInput As String = InputBox("Please enter your password to confirm deletion of this company:", "Confirm Delete")
        If String.IsNullOrEmpty(passwordInput) Then
            MessageBox.Show("Deletion cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Exit Sub
        End If

        If passwordInput <> SessionManager.LoggedInUser.Password Then
            MessageBox.Show("Incorrect password. Deletion aborted.", "Password Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If

        Dim confirmResult As DialogResult = MessageBox.Show("Are you sure you want to permanently delete this company?", "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
        If confirmResult = DialogResult.Yes Then
            Try
                Using conn = GetConnection()
                    conn.Open()
                    Dim sql As String = "DELETE FROM companies WHERE company_id=@id"
                    Using cmd As New SQLiteCommand(sql, conn)
                        cmd.Parameters.AddWithValue("@id", currentComp.CompanyID)
                        cmd.ExecuteNonQuery()
                    End Using
                End Using

                MessageBox.Show("Company deleted successfully.", "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information)

                ' ✅ Refresh the grid if compManagementAdmin is open
                If Application.OpenForms().OfType(Of compManagementAdmin).Any() Then
                    Dim form As compManagementAdmin = Application.OpenForms().OfType(Of compManagementAdmin).First()
                    form.Refresh()
                End If

                ' ✅ Close ONLY this edit form
                Me.Close()

            Catch ex As Exception
                MessageBox.Show("Error deleting company: " & ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If
    End Sub



    Private Sub backBtn_Click(sender As Object, e As EventArgs) Handles backBtn.Click
        compManagementAdmin.Show()
        Me.Hide()
    End Sub


End Class