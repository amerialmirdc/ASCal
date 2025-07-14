Imports ASCal.SQLiteHelper
Imports System.Data.SQLite

Public Class newCompanyForm

    ' ✅ Unified navbar handler for navigation
    Private Sub HandleNavbarClick(sender As Object, e As EventArgs) Handles PictureBox1.Click, Button2.Click, userManagementBtn.Click, compMan.Click, logoutBtn.Click, Button1.Click

        calibrate.RefreshData()
        Me.Close()

        Select Case True
            Case sender Is PictureBox1 OrElse sender Is PictureBox1
                landingPageAdmin.Show()
            Case sender Is Button2
                jobDashAdmin.Show()
            Case sender Is userManagementBtn
                userManagementAdmin.Show()
            Case sender Is compMan
                compManagementAdmin.Show()
            Case sender Is logoutBtn
                login.Show()
            Case sender Is Button1
                dmmManagementAdmin.Show()
        End Select

    End Sub


    Private Sub newCompanyForm_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load

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


        InitializePlaceholders()

    End Sub

    ' ✅ Helper function placeholders
    Private Sub InitializePlaceholders()
        AddPlaceholder(newNameComp, "Enter Company Name")
        AddPlaceholder(newAddComp, "Enter Company Address")
        AddPlaceholder(newEmailComp, "Enter Active Email")
        AddPlaceholder(newContNameComp, "Enter Contact Person")
        AddPlaceholder(newContNumComp, "Enter Contact Person' contact number")
        AddPlaceholder(newDesigComp, "Enter Contact Number Designation")
    End Sub

    ' ✅ Helper function na naglalagay at nagtatanggal ng placeholder kapag focus/leave
    Private Sub AddPlaceholder(txtBox As TextBox, placeholder As String)
        txtBox.Text = placeholder
        txtBox.ForeColor = Color.Gray

        AddHandler txtBox.Enter, Sub()
                                     If txtBox.Text = placeholder Then
                                         txtBox.Text = ""
                                         txtBox.ForeColor = Color.Black
                                     End If
                                 End Sub

        AddHandler txtBox.Leave, Sub()
                                     If String.IsNullOrWhiteSpace(txtBox.Text) Then
                                         txtBox.Text = placeholder
                                         txtBox.ForeColor = Color.Gray
                                     End If
                                 End Sub
    End Sub


    Private Sub newSaveBtn_Click(sender As System.Object, e As System.EventArgs) Handles newSaveBtn.Click
        ' Extract input values from the form
        Dim name As String = newNameComp.Text.Trim()
        Dim address As String = newAddComp.Text.Trim()
        Dim email As String = newEmailComp.Text.Trim()
        Dim contactPerson As String = newContNameComp.Text.Trim()
        Dim contactNumber As String = newContNumComp.Text.Trim()
        Dim designation As String = newDesigComp.Text.Trim()
        Dim dateEnrolled As String = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim status As String = "active"

        ' 🔍 Highlight missing fields
        Dim hasMissingField As Boolean = False
        If HighlightFieldIfEmpty(newNameComp) Then hasMissingField = True
        If HighlightFieldIfEmpty(newAddComp) Then hasMissingField = True
        If HighlightFieldIfEmpty(newEmailComp) Then hasMissingField = True
        If HighlightFieldIfEmpty(newContNameComp) Then hasMissingField = True
        If HighlightFieldIfEmpty(newContNumComp) Then hasMissingField = True
        If HighlightFieldIfEmpty(newDesigComp) Then hasMissingField = True

        If hasMissingField Then
            MessageBox.Show("Please complete all required fields.", "Missing Info", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' 🔍 Validate email format
        If Not System.Text.RegularExpressions.Regex.IsMatch(email, "^[^@\s]+@[^@\s]+\.[^@\s]+$") Then
            MessageBox.Show("Please enter a valid email address.", "Invalid Email", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            newEmailComp.BackColor = Color.MistyRose
            newEmailComp.Focus()
            Exit Sub
        End If

        If contactNumber.Length < 7 Or contactNumber.Length > 15 Then
            MessageBox.Show("Please enter a valid contact number.", "Invalid Number", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            newContNumComp.BackColor = Color.MistyRose
            newContNumComp.Focus()
            Exit Sub
        End If


        Try
            Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                conn.Open()

                ' 🔍 Check for existing company name (case-insensitive)
                Dim checkSql As String = "SELECT COUNT(*) FROM companies WHERE LOWER(company_name) = LOWER(@name)"
                Using checkCmd As New SQLiteCommand(checkSql, conn)
                    checkCmd.Parameters.AddWithValue("@name", name)
                    Dim count As Integer = Convert.ToInt32(checkCmd.ExecuteScalar())

                    If count > 0 Then
                        MessageBox.Show("A company with this name already exists.", "Duplicate Entry", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                        newNameComp.BackColor = Color.MistyRose
                        newNameComp.Focus()
                        Exit Sub
                    End If
                End Using

                ' ✅ Proceed with insert
                Dim sql As String = "INSERT INTO companies (company_name, address, contact_person, contact_number, email, date_enrolled, status) VALUES (@name, @address, @person, @number, @email, @enrolled, @status)"
                Using cmd As New SQLiteCommand(sql, conn)
                    cmd.Parameters.AddWithValue("@name", name)
                    cmd.Parameters.AddWithValue("@address", address)
                    cmd.Parameters.AddWithValue("@person", contactPerson)
                    cmd.Parameters.AddWithValue("@number", contactNumber)
                    cmd.Parameters.AddWithValue("@email", email)
                    cmd.Parameters.AddWithValue("@enrolled", dateEnrolled)
                    cmd.Parameters.AddWithValue("@status", status)

                    cmd.ExecuteNonQuery()
                End Using
            End Using

            MessageBox.Show("Company record saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
            InitializePlaceholders()

        Catch ex As Exception
            MessageBox.Show("Error saving company: " & ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub


    Private Sub backBtn_Click(sender As System.Object, e As System.EventArgs) Handles backBtn.Click
        compManagementAdmin.Show()
        Me.Hide()
    End Sub

    ' ✅ Highlight fields kung empty
    Private Function HighlightFieldIfEmpty(txtBox As TextBox) As Boolean
        If String.IsNullOrWhiteSpace(txtBox.Text) Or txtBox.ForeColor = Color.Gray Then
            txtBox.BackColor = Color.MistyRose
            Return True ' means empty
        Else
            txtBox.BackColor = Color.White
            Return False
        End If
    End Function


End Class