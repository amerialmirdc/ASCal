Imports System.Data.SQLite
Imports System.Drawing.Imaging
Imports System.IO

' --- Win32 for hiding Snipping Tool + blocking input ---
Imports System.Runtime.InteropServices
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Windows.Automation
Imports System.Windows.Forms ' for Clipboard
Imports AForge.Video.DirectShow

Imports System.Diagnostics   ' Stopwatch, Process
Imports System.Linq          ' Any, FirstOrDefault, OrderByDescending, Skip, Take, Distinct

Public Class calibrate

    ' ---- master switch for all temporary/testing UI & timers ----

    Private companyDict As New Dictionary(Of String, String)

    Private dmmItems As New List(Of Tuple(Of String, String, String))
    Private dmmParametersDict As New Dictionary(Of String, List(Of String))

    Private videoSource As VideoCaptureDevice
    Private snipBuffer As New RichTextBox With {.Visible = False}

    ' === OCR helper state ===
    Private latestFrame As Bitmap

    Private latestFrameLock As New Object()

    ' add at class level once:
    Private suppressSelectionEvents As Boolean = False

    ' --- Persist a single Snipping Tool instance ---
    Private snipProc As Process = Nothing

    Private snipReady As Boolean = False

#Region "Navbar and Form Load"

    Private Sub HandleNavClick(sender As Object, e As EventArgs) Handles logoutBtn.Click, logoBtn.Click, jobDashBtn.Click
        contextMenuCompanies.SelectedIndex = -1
        contextMenuCompanies.Text = ""
        dmmSearch.Clear()

        Select Case True
            Case sender Is logoBtn
                landingPageTechnician.Show()
                Me.Close()
            Case sender Is logoutBtn
                login.Show()
                Me.Close()
            Case sender Is jobDashBtn
                jobDashTech.Show()
                Me.Close()
        End Select
    End Sub

    Private Sub calibrate_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Window sizing/placement
        Me.StartPosition = FormStartPosition.Manual
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)
        Me.Bounds = Screen.FromControl(Me).WorkingArea

        LoadDMMsAndParameters()

        If snipBuffer.Parent Is Nothing Then Me.Controls.Add(snipBuffer)

        contextMenuCompanies.Items.Clear()
        For Each companyName As String In companyDict.Keys
            contextMenuCompanies.Items.Add(companyName)
        Next
        contextMenuCompanies.AutoCompleteMode = AutoCompleteMode.SuggestAppend
        contextMenuCompanies.AutoCompleteSource = AutoCompleteSource.ListItems

        technicalID.Text = landingPageTechnician.TechnicianInitials

        range.Text = "See Specification Sheet"
        readability.Text = "See Specification Sheet"
        accuracy.Text = "See Specification Sheet"

        ' Display a work order number (read-only display only)
        workOrderNo.Text = SQLiteHelper.GenerateNextWorkOrderNumber()

        dataGridResultDMM.DefaultCellStyle.Font = New Font("Courier10 BT", 12)
        dataGridResultDMM.ColumnHeadersDefaultCellStyle.Font = New Font("Courier10 BT", 12)
        dataGridResultDMM.RowHeadersVisible = False

        ' after you set fonts etc.
        EnsureBaseGridColumns()
        PopulateDataGrid()

        dataGridResultDMM.ClearSelection()
        cLParamACV.Font = New Font("Courier10 BT", 14, FontStyle.Regular)
        cLParamDCV.Font = New Font("Courier10 BT", 14, FontStyle.Regular)
        cLParamACC.Font = New Font("Courier10 BT", 14, FontStyle.Regular)
        cLParamDCC.Font = New Font("Courier10 BT", 14, FontStyle.Regular)
        cLParamRES.Font = New Font("Courier10 BT", 14, FontStyle.Regular)

        ' ----- Camera init (prefers external webcam) -----
        Dim cam = CreatePreferredCamera()
        If cam IsNot Nothing Then
            videoSource = cam
            AddHandler videoSource.NewFrame, AddressOf VideoSource_NewFrame
            videoSource.Start()

            ' --- NEW: Ask the user to capture once camera is started ---
            Dim resp = MessageBox.Show(
            "Camera is ready. Do you want to capture now?",
            "Capture",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question
        )

            If resp = DialogResult.OK Then
                ' Option A (no refactor): reuse the existing click handler
                BtnCapture_Click(BtnCapture, EventArgs.Empty)

                ' Option B (if you created a helper like PerformCapture()):
                ' PerformCapture()
            End If
            ' -------------------------------------------------------------
        Else
            MessageBox.Show("No camera devices found.")
        End If

        For Each row As DataGridViewRow In dataGridResultDMM.Rows
            If Not row.IsNewRow Then
                Dim modelValue = row.Cells("MODEL").Value
                If modelValue IsNot Nothing AndAlso modelValue.ToString() = "FLUKE 0001" Then
                    row.Selected = True
                    Exit For
                End If
            End If
        Next

        ' Load company list from DB
        LoadCompaniesFromDatabase()

        contextMenuCompanies.Items.Clear()
        For Each companyName As String In companyDict.Keys
            contextMenuCompanies.Items.Add(companyName)
        Next

        contextMenuCompanies.AutoCompleteMode = AutoCompleteMode.SuggestAppend
        contextMenuCompanies.AutoCompleteSource = AutoCompleteSource.ListItems

        ' Standardize table layout controls
        For row As Integer = 0 To TableLayoutPanel1.RowCount - 1
            For col As Integer = 0 To 3
                Dim ctrl As Control = TableLayoutPanel1.GetControlFromPosition(col, row)
                If TypeOf ctrl Is TextBox Then
                    With DirectCast(ctrl, TextBox)
                        .Width = 150
                        .Anchor = AnchorStyles.Left
                        .Dock = DockStyle.Fill
                        .Margin = New Padding(5)
                        .Font = New Font("Courier New", 10, FontStyle.Regular)
                    End With
                ElseIf TypeOf ctrl Is DateTimePicker Then
                    ctrl.Dock = DockStyle.Fill
                    ctrl.Margin = New Padding(5)
                End If
            Next
        Next

        HookPanelWheel(mainPanelCalibrateInp)
    End Sub

#End Region

#Region "Camera Related"

    Private Sub VideoSource_NewFrame(sender As Object, eventArgs As AForge.Video.NewFrameEventArgs)
        Dim frame As Bitmap = Nothing
        Try
            frame = DirectCast(eventArgs.Frame.Clone(), Bitmap)

            ' Save latest frame for OCR
            SyncLock latestFrameLock
                If latestFrame IsNot Nothing Then latestFrame.Dispose()
                latestFrame = DirectCast(frame.Clone(), Bitmap)
            End SyncLock

            ' Update PictureBox safely
            Dim displayFrame As Bitmap = DirectCast(frame.Clone(), Bitmap)
            If PictureBox1.InvokeRequired Then
                PictureBox1.BeginInvoke(Sub()
                                            If PictureBox1.Image IsNot Nothing Then PictureBox1.Image.Dispose()
                                            PictureBox1.Image = displayFrame
                                        End Sub)
            Else
                If PictureBox1.Image IsNot Nothing Then PictureBox1.Image.Dispose()
                PictureBox1.Image = displayFrame
            End If
        Catch ex As Exception
            ' Optional: log or debug
        Finally
            If frame IsNot Nothing Then frame.Dispose()
        End Try
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If videoSource IsNot Nothing AndAlso videoSource.IsRunning Then
            Try
                RemoveHandler videoSource.NewFrame, AddressOf VideoSource_NewFrame
                videoSource.SignalToStop()
                videoSource.WaitForStop()
            Catch
            End Try
        End If
        MyBase.OnFormClosing(e)
    End Sub

    Private Function CreatePreferredCamera() As VideoCaptureDevice
        Dim devices = New FilterInfoCollection(FilterCategory.VideoInputDevice)
        If devices Is Nothing OrElse devices.Count = 0 Then Return Nothing

        Dim externalKeywords = New String() {
            "logi", "logitech", "brio", "c920", "c922", "c925", "c930",
            "microsoft", "lifecam", "creative", "razer", "elgato", "aver",
            "aukey", "hd pro", "usb", "webcam hd", "camera hd"
        }
        Dim internalKeywords = New String() {
            "integrated", "internal", "built-in", "builtin", "laptop",
            "hd camera", "front camera"
        }

        Dim pick As FilterInfo = Nothing

        For Each d As FilterInfo In devices
            Dim n = d.Name.ToLowerInvariant()
            If externalKeywords.Any(Function(k) n.Contains(k)) Then
                pick = d : Exit For
            End If
        Next

        If pick Is Nothing Then
            For Each d As FilterInfo In devices
                Dim n = d.Name.ToLowerInvariant()
                If Not internalKeywords.Any(Function(k) n.Contains(k)) Then
                    pick = d : Exit For
                End If
            Next
        End If

        If pick Is Nothing Then pick = devices(0)

        Dim cam = New VideoCaptureDevice(pick.MonikerString)
        Try
            Dim caps = cam.VideoCapabilities
            If caps IsNot Nothing AndAlso caps.Length > 0 Then
                Dim best = caps.FirstOrDefault(Function(c) c.FrameSize.Width = 1280 AndAlso c.FrameSize.Height = 720)
                If best Is Nothing Then
                    best = caps.OrderByDescending(Function(c) c.FrameSize.Width * c.FrameSize.Height).First()
                End If
                cam.VideoResolution = best
            End If
        Catch
        End Try

        Return cam
    End Function

    Private Sub BtnCapture_Click(sender As Object, e As EventArgs) Handles BtnCapture.Click
        BtnCapture.Enabled = False
        Dim tempPath As String = ""
        Try
            ' 1) Grab the most recent frame to a temp file (no camera stop/start)
            tempPath = SaveLatestFrameToTemp()
            If String.IsNullOrEmpty(tempPath) OrElse Not File.Exists(tempPath) Then
                MessageBox.Show("No camera frame available for OCR.", "OCR", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Exit Sub
            End If
            capturePath = tempPath

            ' 2) OCR via your current Snipping Tool routine
            CaptureAndSnipOcr()

            ' Early exit if OCR found nothing
            If String.IsNullOrWhiteSpace(raw) Then
                dataGridResultDMM.ClearSelection()
                Exit Sub
            End If

            ' 3) Candidate search: direct DB hit (brand+model) then fuzzy fallbacks
            Dim scored As New List(Of Tuple(Of Double, Tuple(Of String, String, String)))()

            Dim brands = LoadManufacturerList()
            Dim guess = ExtractBrandAndModelFromOcr(raw, brands)
            Dim direct = LookupDmmInDatabase(guess.Item1, guess.Item2)
            If direct IsNot Nothing Then
                scored.Add(Tuple.Create(100.0, Tuple.Create(direct.Item1, direct.Item2, direct.Item3)))
            End If

            Dim ocrNorm As String = NormalizeText(raw)
            For Each d In dmmItems
                Dim s As Double = ScoreDmmByOcr(ocrNorm, d)
                If s > 0 Then scored.Add(Tuple.Create(s, d))
            Next

            ' 4) Present best matches + apply top choice to UI
            If scored.Count > 0 Then
                scored = scored.OrderByDescending(Function(t) t.Item1).ToList()

                ShowTopMatchesInGrid(scored, 5)

                Dim best = scored(0).Item2  ' (Model, Manufacturer, Description)
                ApplyDmmToUi(best.Item1, best.Item2, best.Item3)

                dataGridResultDMM.ClearSelection()
                If dataGridResultDMM.Rows.Count > 0 Then
                    dataGridResultDMM.Rows(0).Selected = True
                    dataGridResultDMM.CurrentCell = dataGridResultDMM.Rows(0).Cells(0)
                End If
            Else
                dataGridResultDMM.ClearSelection()
            End If
        Finally
            ' 5) Clean up the temp image
            Try
                If Not String.IsNullOrEmpty(tempPath) AndAlso File.Exists(tempPath) Then File.Delete(tempPath)
            Catch
            End Try
            capturePath = ""
            BtnCapture.Enabled = True
        End Try
    End Sub

#End Region

#Region "Serial of prev. cert"

    Private Sub serialNumber_change(sender As Object, e As EventArgs) Handles serialNumber.TextChanged, serialNumber.Leave, serialNumber.KeyDown
        If TypeOf e Is KeyEventArgs Then
            Dim ke As KeyEventArgs = DirectCast(e, KeyEventArgs)
            If ke.KeyCode <> Keys.Enter Then Return
        End If

        Dim selectedCompany As String = contextMenuCompanies.Text.Trim()
        If companyDict.ContainsKey(selectedCompany) Then
            compAdd.Text = companyDict(selectedCompany)
        Else
            compAdd.Clear()
        End If

        Dim sn As String = serialNumber.Text.Trim()
        If sn = "" Then
            prevCalCert.Text = "NA"
            prevTech.Text = "NA"
            Exit Sub
        End If

        Try
            Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                conn.Open()
                Dim query As String = "
                SELECT workOrderNumber, technician_name
                FROM calibration_jobs
                WHERE UPPER(serial_number) = @sn
                ORDER BY calibration_date DESC
                LIMIT 1"
                Using cmd As New SQLiteCommand(query, conn)
                    cmd.Parameters.AddWithValue("@sn", sn.ToUpper())
                    Using reader As SQLiteDataReader = cmd.ExecuteReader()
                        If reader.Read() Then
                            prevCalCert.Text = reader("workOrderNumber").ToString()
                            prevTech.Text = reader("technician_name").ToString()
                        Else
                            prevCalCert.Text = "NA"
                            prevTech.Text = "NA"
                        End If
                    End Using
                End Using
            End Using
        Catch ex As Exception
            prevCalCert.Text = "No input"
            prevTech.Text = "No input"
        End Try
    End Sub

#End Region

#Region "Company Related"

    Private Sub contextMenuCompanies_SelectedIndexChanged(sender As Object, e As EventArgs) Handles contextMenuCompanies.SelectedIndexChanged
        Dim selectedCompany As String = contextMenuCompanies.Text.Trim()
        If companyDict.ContainsKey(selectedCompany) Then
            compAdd.Text = companyDict(selectedCompany)
        Else
            compAdd.Clear()
        End If
    End Sub

    Private Sub LoadCompaniesFromDatabase()
        companyDict.Clear()
        Try
            Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                conn.Open()
                Dim sql As String = "SELECT company_name, address FROM companies WHERE status='active' ORDER BY company_name ASC"
                Using cmd As New SQLiteCommand(sql, conn)
                    Using reader As SQLiteDataReader = cmd.ExecuteReader()
                        While reader.Read()
                            Dim name As String = reader("company_name").ToString().Trim()
                            Dim address As String = reader("address").ToString().Trim()
                            If Not companyDict.ContainsKey(name) Then
                                companyDict.Add(name, address)
                            End If
                        End While
                    End Using
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Failed to load companies: " & ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub CheckedListBox1_MouseUp(sender As Object, e As MouseEventArgs) Handles CheckedListBox1.MouseUp
        Dim index As Integer = CheckedListBox1.IndexFromPoint(e.Location)
        If index <> ListBox.NoMatches Then
            For i As Integer = 0 To CheckedListBox1.Items.Count - 1
                CheckedListBox1.SetItemChecked(i, False)
            Next
            CheckedListBox1.SetItemChecked(index, True)
            CheckedListBox1.ClearSelected()
            If CheckedListBox1.GetItemChecked(1) Then
                specificSite.Enabled = True
                specificSite.Text = compAdd.Text
            Else
                specificSite.Enabled = False
                specificSite.Clear()
            End If
        End If
    End Sub

    Private Sub contextMenuCompanies_TextChanged(sender As Object, e As EventArgs) Handles contextMenuCompanies.TextChanged
        Dim typedCompany As String = contextMenuCompanies.Text.Trim()
        If companyDict.ContainsKey(typedCompany) Then
            compAdd.Text = companyDict(typedCompany)
        Else
            compAdd.Clear()
        End If
    End Sub

#End Region

#Region "DMM Related"

    Private Sub LoadDMMsAndParameters()
        dmmItems.Clear()
        dmmParametersDict.Clear()

        Try
            Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                conn.Open()

                Dim modelCmd As New SQLiteCommand("SELECT DISTINCT model_name, manufacturer, description FROM dmm ORDER BY model_name ASC", conn)
                Using reader As SQLiteDataReader = modelCmd.ExecuteReader()
                    While reader.Read()
                        Dim model As String = reader("model_name").ToString()
                        Dim manufacturer As String = reader("manufacturer").ToString()
                        Dim description As String = reader("description").ToString()
                        dmmItems.Add(New Tuple(Of String, String, String)(model, manufacturer, description))
                    End While
                End Using

                Dim paramSql As String = ""
                paramSql &= "SELECT dmm.model_name, parameter_categories.name AS category_name "
                paramSql &= "FROM dmm_ranges "
                paramSql &= "INNER JOIN dmm ON dmm_ranges.dmm_id = dmm.id "
                paramSql &= "INNER JOIN parameter_categories ON dmm_ranges.category_id = parameter_categories.id "

                Dim paramCmd As New SQLiteCommand(paramSql, conn)
                Using paramReader As SQLiteDataReader = paramCmd.ExecuteReader()
                    While paramReader.Read()
                        Dim model As String = paramReader("model_name").ToString()
                        Dim category As String = paramReader("category_name").ToString()

                        If Not dmmParametersDict.ContainsKey(model) Then
                            dmmParametersDict(model) = New List(Of String)
                        End If
                        If Not dmmParametersDict(model).Contains(category) Then
                            dmmParametersDict(model).Add(category)
                        End If
                    End While
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading DMMs and parameters: " & ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub dataGridResult_CellClick(sender As Object, e As DataGridViewCellEventArgs) Handles dataGridResultDMM.CellClick
        If e.RowIndex < 0 Then Exit Sub

        Dim selectedRow As DataGridViewRow = dataGridResultDMM.Rows(e.RowIndex)
        Dim selectedModel As String = selectedRow.Cells(0).Value.ToString()

        Dim dmm = dmmItems.FirstOrDefault(Function(i) i.Item1 = selectedModel)
        If dmm Is Nothing Then
            MessageBox.Show("DMM not found in master list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ApplyDmmToUi(dmm.Item1, dmm.Item2, dmm.Item3)

    End Sub

#End Region

#Region "Checkbox for Parameter"

    Private Function GetCheckedListBoxForCategory(category As String) As CheckedListBox
        Select Case category.Trim().ToUpper()
            Case "AC VOLTAGE" : Return cLParamACV
            Case "DC VOLTAGE" : Return cLParamDCV
            Case "AC CURRENT" : Return cLParamACC
            Case "DC CURRENT" : Return cLParamDCC
            Case "RESISTANCE" : Return cLParamRES
            Case Else : Return Nothing
        End Select
    End Function

    Private Sub dataGridResult_CellMouseMove(sender As Object, e As DataGridViewCellMouseEventArgs) Handles dataGridResultDMM.CellMouseMove
        dataGridResultDMM.Cursor = If(e.RowIndex >= 0, Cursors.Hand, Cursors.Default)
    End Sub

    Private Sub PopulateDataGrid(Optional ByVal filter As String = "")
        EnsureBaseGridColumns()
        dataGridResultDMM.Rows.Clear()
        For Each item In dmmItems
            If filter = "" OrElse item.Item1.ToLower().Contains(filter.ToLower()) Then
                dataGridResultDMM.Rows.Add(item.Item1, item.Item2)
            End If
        Next
    End Sub

    Private Sub dmmSearch_TextChanged(sender As Object, e As EventArgs) Handles dmmSearch.TextChanged
        PopulateDataGrid(dmmSearch.Text)
    End Sub

    Private Sub dataGridResult_SelectionChanged(sender As Object, e As EventArgs) Handles dataGridResultDMM.SelectionChanged
        If suppressSelectionEvents Then Exit Sub
        If dataGridResultDMM.SelectedRows.Count > 0 Then
            Dim selectedModel As String = dataGridResultDMM.SelectedRows(0).Cells("MODEL").Value.ToString()
            Dim selectedItem As Tuple(Of String, String, String) = dmmItems.FirstOrDefault(Function(i) i.Item1 = selectedModel)
            If selectedItem IsNot Nothing Then
                ApplyDmmToUi(selectedItem.Item1, selectedItem.Item2, selectedItem.Item3)
            End If
        End If
    End Sub

    Private Sub HandleCheckedListBoxClick(clb As CheckedListBox, e As MouseEventArgs)
        Dim index As Integer = clb.IndexFromPoint(e.Location)
        If index < 0 Then Exit Sub

        Dim trimmed = clb.Items(index).ToString().TrimStart()

        Dim toggleChecked = Sub(i As Integer, isChecked As Boolean)
                                clb.SetItemChecked(i, isChecked)
                            End Sub

        If trimmed.StartsWith("[") Then
            Dim isChecked = clb.GetItemChecked(index)
            Dim i As Integer = index + 1
            While i < clb.Items.Count AndAlso Not clb.Items(i).ToString().TrimStart().StartsWith("[")
                toggleChecked(i, isChecked)
                i += 1
            End While
        ElseIf trimmed.StartsWith("→ Range:") Then
            Dim isChecked = clb.GetItemChecked(index)
            Dim i As Integer = index + 1
            While i < clb.Items.Count AndAlso clb.Items(i).ToString().TrimStart().StartsWith("→ Nominal:")
                toggleChecked(i, isChecked)
                i += 1
            End While
        Else
            Dim isChecked = Not clb.GetItemChecked(index)
            toggleChecked(index, isChecked)
        End If
    End Sub

    Private Sub cLParams_MouseUp(sender As Object, e As MouseEventArgs) _
    Handles cLParamACV.MouseUp,
            cLParamDCV.MouseUp,
            cLParamACC.MouseUp,
            cLParamDCC.MouseUp,
            cLParamRES.MouseUp

        HandleCheckedListBoxClick(DirectCast(sender, CheckedListBox), e)

    End Sub

    Private Sub SetAllItemsChecked(isChecked As Boolean)
        For Each clb As CheckedListBox In {cLParamACV, cLParamDCV, cLParamACC, cLParamDCC, cLParamRES}
            For i As Integer = 0 To clb.Items.Count - 1
                clb.SetItemChecked(i, isChecked)
            Next
        Next
    End Sub

    Private Sub btnSelectAll_Click(sender As Object, e As EventArgs) Handles btnSelectAll.Click
        SetAllItemsChecked(True)
    End Sub

    Private Sub btnUnselectAll_Click(sender As Object, e As EventArgs) Handles btnUnselectAll.Click
        SetAllItemsChecked(False)
    End Sub

#End Region

#Region "Required Fields"

    Private Function AllInputsFilledInPanel(panel As Panel) As Boolean
        Dim excludedFields As New List(Of String) From {"dmmSearch", "specificSite", "refstand4", "DateTimePicker1", "TextBox23", "TextBox21", "TextBox19", "TextBox25", "refstand3", "refstand2", "refstand2", "refstand6", "refstand5", "refstand4", "DateTimePicker1", "TextBox31", "TextBox19", "TextBox27", "TextBox25", "TextBox28", "TextBox26", "TextBox29", "TextBox30", "TextBox20", "TextBox22", "TextBox24", "compAdd"}
        For Each ctrl As Control In panel.Controls
            If TypeOf ctrl Is TextBox AndAlso Not excludedFields.Contains(ctrl.Name) Then
                If String.IsNullOrWhiteSpace(ctrl.Text) Then
                    ctrl.BackColor = Color.MistyRose
                    MessageBox.Show("Please complete all required fields.", "Incomplete", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    ctrl.Focus()
                    Return False
                Else
                    ctrl.BackColor = Color.White
                End If
            End If
        Next

        If String.IsNullOrWhiteSpace(contextMenuCompanies.Text) OrElse Not companyDict.ContainsKey(contextMenuCompanies.Text.Trim()) Then
            MessageBox.Show("Please select a valid calibration company from the list.", "Missing Company", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            contextMenuCompanies.Focus()
            Return False
        End If

        Dim anyChecked As Boolean =
            (cLParamACV.CheckedItems.Count > 0) OrElse
            (cLParamDCV.CheckedItems.Count > 0) OrElse
            (cLParamACC.CheckedItems.Count > 0) OrElse
            (cLParamDCC.CheckedItems.Count > 0) OrElse
            (cLParamRES.CheckedItems.Count > 0)

        If Not anyChecked Then
            MessageBox.Show("Please select at least one calibration parameter.", "Missing Parameters", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            cLParamACV.Focus()
            Return False
        End If

        Return True
    End Function

#End Region

#Region "Start Calibration button and transfer of data"

    Private Sub btnStartCalibration_Click(sender As Object, e As EventArgs) Handles btnStartCalibration.Click
        If Not AllInputsFilledInPanel(mainPanelCalibrateInp) Then Exit Sub

        Dim allParams As New List(Of String)
        For Each it As Object In cLParamACV.CheckedItems : allParams.Add(it.ToString()) : Next
        For Each it As Object In cLParamDCV.CheckedItems : allParams.Add(it.ToString()) : Next
        For Each it As Object In cLParamACC.CheckedItems : allParams.Add(it.ToString()) : Next
        For Each it As Object In cLParamDCC.CheckedItems : allParams.Add(it.ToString()) : Next
        For Each it As Object In cLParamRES.CheckedItems : allParams.Add(it.ToString()) : Next

        Dim activeCategories As New List(Of String)
        If cLParamACV.CheckedItems.Count > 0 Then activeCategories.Add("AC VOLTAGE")
        If cLParamDCV.CheckedItems.Count > 0 Then activeCategories.Add("DC VOLTAGE")
        If cLParamACC.CheckedItems.Count > 0 Then activeCategories.Add("AC CURRENT")
        If cLParamDCC.CheckedItems.Count > 0 Then activeCategories.Add("DC CURRENT")
        If cLParamRES.CheckedItems.Count > 0 Then activeCategories.Add("RESISTANCE")

        Dim cr As New calibratingResult() With {
            .JobId = 0,
            .WorkOrderNumber = workOrderNo.Text,
            .CompanyName = contextMenuCompanies.Text.Trim(),
            .CompanyAddress = compAdd.Text.Trim(),
            .Model = dmmmodel.Text.Trim(),
            .Manufacturer = manufaacturer.Text.Trim(),
            .Description = dmmdescription.Text.Trim(),
            .TechnicianInitials = technicalID.Text.Trim(),
            .TechnicianName = CurrentUser.Name,
            .CalibrationType = If(CheckedListBox1.CheckedItems.Count > 0, CheckedListBox1.CheckedItems(0).ToString(), ""),
            .SpecificSite = specificSite.Text.Trim(),
            .SerialNumber = serialNumber.Text.Trim(),
            .SelectedParameters = allParams,
            .ActiveCategories = activeCategories,
            .Range = range.Text.Trim(),
            .Readability = readability.Text.Trim(),
            .PrevSesCalCert = prevCalCert.Text.Trim(),
            .AccuracyHeader = accuracy.Text.Trim(),
            .PreviousTechnician = prevTech.Text.Trim(),
            .ReceivedDate = receivedDate.Value.ToString("dd-MMM-yyyy"),
            .CalibrationDate = calibrationDate.Value.ToString("dd-MMM-yyyy"),
            .OptionsInstalled = optionsInstalled.Text.Trim(),
            .CustomerPO = customerPO.Text.Trim(),
            .AssetNumber = assetNumber.Text.Trim(),
            .TempStart = txtTempStart.Text.Trim(),
            .TempEnd = txtTempEnd.Text.Trim(),
            .HumidityStart = txtHumidityStart.Text.Trim(),
            .HumidityEnd = txtHumidityEnd.Text.Trim(),
            .RefDesc1 = RefCal_description1.Text.Trim(),
            .RefSN1 = RefCal_serialNo1.Text.Trim(),
            .RefCalRef1 = RefCal_calReportRef1.Text.Trim(),
            .RefDue1 = If(refCal_DueDate1.Enabled, refCal_DueDate1.Value.ToString("dd-MMM-yyyy"), ""),
            .RefDesc2 = RefCal_description2.Text.Trim(),
            .RefSN2 = RefCal_serialNo2.Text.Trim(),
            .RefCalRef2 = RefCal_calReportRef2.Text.Trim(),
            .RefDue2 = If(refCal_DueDate2.Enabled, refCal_DueDate2.Value.ToString("dd-MMM-yyyy"), ""),
            .AccDesc1 = accUsed_Description1.Text.Trim(),
            .AccSN1 = accUsed_SerialNo1.Text.Trim(),
            .AccCalBrand1 = accUsed_Brand1.Text.Trim(),
            .AccModel1 = accUsed_Model1.Text.Trim(),
            .AccDesc2 = accUsed_Description2.Text.Trim(),
            .AccSN2 = accUsed_SerialNo2.Text.Trim(),
            .AccCalBrand2 = accUsed_Brand2.Text.Trim(),
            .AccModel2 = accUsed_Model2.Text.Trim(),
            .calMathod = calMethod.Text.Trim()
        }

        cr.Show()
        Me.Close()
    End Sub

#End Region

#Region "Reference Cal"

    Private Sub RefCal_description1_TextChanged(sender As Object, e As EventArgs) Handles RefCal_description1.TextChanged, RefCal_serialNo1.TextChanged, RefCal_calReportRef1.TextChanged
        UpdateRefDue1State()
    End Sub

    Private Sub RefCal_description2_TextChanged(sender As Object, e As EventArgs) Handles RefCal_description2.TextChanged, RefCal_serialNo2.TextChanged, RefCal_calReportRef2.TextChanged
        UpdateRefDue2State()
    End Sub

    Private Sub UpdateRefDue1State()
        Dim hasData As Boolean =
            Not String.IsNullOrWhiteSpace(RefCal_description1.Text) AndAlso
            Not String.IsNullOrWhiteSpace(RefCal_serialNo1.Text) AndAlso
            Not String.IsNullOrWhiteSpace(RefCal_calReportRef1.Text)
        refCal_DueDate1.Enabled = hasData
    End Sub

    Private Sub UpdateRefDue2State()
        Dim hasData As Boolean =
            Not String.IsNullOrWhiteSpace(RefCal_description2.Text) AndAlso
            Not String.IsNullOrWhiteSpace(RefCal_serialNo2.Text) AndAlso
            Not String.IsNullOrWhiteSpace(RefCal_calReportRef2.Text)
        refCal_DueDate2.Enabled = hasData
    End Sub

#End Region

#Region "Scroll Helper Function"

    Private Function FindScrollableParent(ctrl As Control) As ScrollableControl
        Dim p As Control = ctrl.Parent
        While p IsNot Nothing
            Dim sc = TryCast(p, ScrollableControl)
            If sc IsNot Nothing AndAlso sc.AutoScroll Then Return sc
            p = p.Parent
        End While
        Return Nothing
    End Function

    Private Sub Child_MouseWheelScrollParent(sender As Object, e As MouseEventArgs)
        Dim he = TryCast(e, HandledMouseEventArgs)
        If he IsNot Nothing Then he.Handled = True

        Dim sc = FindScrollableParent(DirectCast(sender, Control))
        If sc Is Nothing Then Exit Sub

        Dim curY = -sc.AutoScrollPosition.Y
        Dim targetY = Math.Max(0, curY - e.Delta)
        sc.AutoScrollPosition = New Point(-sc.AutoScrollPosition.X, targetY)
    End Sub

    Private Sub ForwardWheelToPanel(sender As Object, e As MouseEventArgs, target As ScrollableControl)
        Dim he = TryCast(e, HandledMouseEventArgs)
        If he IsNot Nothing Then he.Handled = True

        If target Is Nothing Then Exit Sub

        Dim curY = -target.AutoScrollPosition.Y
        Dim targetY = Math.Max(0, curY - e.Delta)
        target.AutoScrollPosition = New Point(-target.AutoScrollPosition.X, targetY)
    End Sub

    Private Sub HookPanelWheel(panel As ScrollableControl)
        For Each ctrl As Control In panel.Controls
            AddHandler ctrl.MouseWheel,
                Sub(s, e) ForwardWheelToPanel(s, e, panel)
            If ctrl.HasChildren Then
                HookPanelWheelRecursive(ctrl, panel)
            End If
        Next
    End Sub

    Private Sub HookPanelWheelRecursive(container As Control, targetPanel As ScrollableControl)
        For Each ctrl As Control In container.Controls
            AddHandler ctrl.MouseWheel,
                Sub(s, e) ForwardWheelToPanel(s, e, targetPanel)
            If ctrl.HasChildren Then
                HookPanelWheelRecursive(ctrl, targetPanel)
            End If
        Next
    End Sub

#End Region

    Public Sub RefreshData()
        contextMenuCompanies.Items.Clear()
        companyDict.Clear()
        Dim companies = LoadAllCompanies()
        For Each comp In companies
            contextMenuCompanies.Items.Add(comp.Name)
        Next
    End Sub

#Region "OCR"

    ' ==== Add near your other P/Invokes ====
    <DllImport("user32.dll")>
    Private Shared Function ShowWindowAsync(hWnd As IntPtr, nCmdShow As Integer) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function SetWindowPos(hWnd As IntPtr, hWndInsertAfter As IntPtr, X As Integer, Y As Integer, cx As Integer, cy As Integer, uFlags As UInteger) As Boolean
    End Function

    Private Const SWP_NOSIZE As UInteger = &H1UI
    Private Const SWP_NOZORDER As UInteger = &H4UI
    Private Const SWP_HIDEWINDOW As UInteger = &H80UI

    Private Sub HideSnippingToolRobust(Optional totalTimeoutMs As Integer = 4000)
        Dim procNames = {"SnippingTool", "SnipAndSketch"}
        Dim sw As New Stopwatch()
        sw.Start()

        Do
            Dim anyFound As Boolean = False

            For Each procName As String In procNames
                For Each p As Process In Process.GetProcessesByName(procName)
                    Dim h = p.MainWindowHandle
                    If h <> IntPtr.Zero Then
                        anyFound = True
                        ' Try both hide approaches in case one no-ops on this build
                        ShowWindowAsync(h, SW_HIDE)
                        ' Move far off-screen as an extra safeguard
                        SetWindowPos(h, IntPtr.Zero, -20000, -20000, 0, 0,
                                 SWP_NOSIZE Or SWP_NOZORDER Or SWP_HIDEWINDOW)
                    End If
                Next
            Next

            ' Keep polling briefly to catch late windows
            Thread.Sleep(50)
        Loop While sw.ElapsedMilliseconds < totalTimeoutMs
    End Sub

    ' --- Win32 for hiding Snipping Tool + blocking input ---
    <DllImport("user32.dll")>
    Private Shared Function ShowWindow(hWnd As IntPtr, nCmdShow As Integer) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function BlockInput(fBlockIt As Boolean) As Boolean
    End Function

    Private Const SW_HIDE As Integer = 0
    Private Const SW_SHOW As Integer = 5

    Private Sub HideSnippingTool()
        Dim snippingProcesses As String() = {"SnippingTool", "SnipAndSketch"}
        For Each procName As String In snippingProcesses
            For Each p As Process In Process.GetProcessesByName(procName)
                Dim h As IntPtr = p.MainWindowHandle
                If h <> IntPtr.Zero Then ShowWindow(h, SW_HIDE)
            Next
        Next
    End Sub

    Private Function EnsureSnippingToolRunning() As Boolean
        If snipProc IsNot Nothing AndAlso Not snipProc.HasExited Then Return True

        ' Try to attach to an existing instance first
        For Each p In Process.GetProcessesByName("SnippingTool")
            If Not p.HasExited Then
                snipProc = p
                Exit For
            End If
        Next

        If snipProc Is Nothing OrElse snipProc.HasExited Then
            Try
                Dim psi As New ProcessStartInfo With {
                .FileName = "snippingtool.exe",
                .UseShellExecute = True,
                .WindowStyle = ProcessWindowStyle.Minimized
            }
                snipProc = Process.Start(psi)
            Catch
                Return False
            End Try
        End If

        Try : snipProc.WaitForInputIdle(2000) : Catch : End Try
        HideSnippingToolRobust(800) ' you already have this helper
        snipReady = True
        Return True
    End Function

    Private Function WaitForClipboardText(timeoutMs As Integer) As String
        Dim sw As Stopwatch = Stopwatch.StartNew()
        Dim txt As String = ""
        Do
            Try
                If Clipboard.ContainsText() Then
                    txt = Clipboard.GetText()
                    If Not String.IsNullOrWhiteSpace(txt) Then Exit Do
                End If
            Catch
                ' transient clipboard ownership errors are normal—ignore and retry
            End Try
            Thread.Sleep(35)
        Loop While sw.ElapsedMilliseconds < timeoutMs
        Return txt
    End Function

    Private Sub RemoveFocus()
        ' any harmless control to absorb focus; fallback to the form
        Dim c As Control = Me
        c.Focus()
    End Sub

    ' Optional: slightly friendlier OCR normalization
    Private Function NormalizeOcrText(txt As String) As String
        If String.IsNullOrWhiteSpace(txt) Then Return ""
        Dim t = txt.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf)
        t = System.Text.RegularExpressions.Regex.Replace(t, "[ \t]+", " ")
        t = System.Text.RegularExpressions.Regex.Replace(t, "\n{2,}", vbLf)
        Return t.Trim()
    End Function

    ' ---- OCR state ----
    Private capturePath As String = ""      ' file path of the image to OCR

    Private raw As String = ""              ' last raw OCR text

    ' Save the latest camera frame to a temp PNG for OCR
    Private Function SaveLatestFrameToTemp() As String
        Dim out As String = ""
        SyncLock latestFrameLock
            If latestFrame Is Nothing Then Return ""
            Try
                out = Path.Combine(Path.GetTempPath(), "ascal_ocr_" & Guid.NewGuid().ToString("N") & ".png")
                latestFrame.Save(out, ImageFormat.Png)
            Catch
                out = ""
            End Try
        End SyncLock
        Return out
    End Function

    Private Sub CaptureAndSnipOcr()
        If String.IsNullOrWhiteSpace(capturePath) OrElse Not File.Exists(capturePath) Then
            MessageBox.Show("No image available for OCR (capturePath missing).", "OCR",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning)
            raw = ""
            Exit Sub
        End If

        raw = RunOcrOnImage(capturePath)
    End Sub

    Private Sub CloseSnippingToolInstances(Optional timeoutMs As Integer = 1000)
        For Each n In {"SnippingTool", "SnipAndSketch"}
            For Each p As Process In Process.GetProcessesByName(n)
                Try
                    If Not p.HasExited Then
                        p.CloseMainWindow()
                        If Not p.WaitForExit(timeoutMs) Then p.Kill()
                    End If
                Catch
                End Try
            Next
        Next
    End Sub

    ' Ensure MODEL + MANUFACTURER columns exist (and keep SCORE if present)
    Private Sub EnsureBaseGridColumns()
        ' If columns missing or were cleared, recreate the two data columns
        Dim needsReset As Boolean =
            (dataGridResultDMM.Columns.Count = 0) OrElse
            (Not dataGridResultDMM.Columns.Contains("MODEL")) OrElse
            (Not dataGridResultDMM.Columns.Contains("MANUFACTURER"))

        If needsReset Then
            dataGridResultDMM.Columns.Clear()
            dataGridResultDMM.AutoGenerateColumns = False

            Dim colModel As New DataGridViewTextBoxColumn With {
            .Name = "MODEL", .HeaderText = "MODEL", .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 55
        }
            Dim colMfg As New DataGridViewTextBoxColumn With {
            .Name = "MANUFACTURER", .HeaderText = "MANUFACTURER", .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 45
        }

            dataGridResultDMM.Columns.Add(colModel)
            dataGridResultDMM.Columns.Add(colMfg)

            ' Optional styling (you already set these in Load)
            dataGridResultDMM.RowHeadersVisible = False
        End If
    End Sub

    ' TODO: plug in your real OCR here (Tesseract/Windows OCR/etc.)
    ' Snipping Tool OCR that returns text (no UI side-effects)
    Private Function RunOcrOnImage(imagePath As String) As String
        If String.IsNullOrWhiteSpace(imagePath) OrElse Not File.Exists(imagePath) Then Return ""

        ' Reset clipboard + buffer
        Try
            If snipBuffer IsNot Nothing Then snipBuffer.Clear()
            Clipboard.Clear()
        Catch
        End Try

        RemoveFocus()
        BlockInput(True)
        Dim unblocked As Boolean = False

        Dim snipProc As Process = Nothing
        Try
            ' Fresh instance each run so keystrokes hit a known UI state
            CloseSnippingToolInstances(700)

            ' Launch minimized
            Dim launched As Boolean = False
            Try
                Dim psi As New ProcessStartInfo()
                psi.FileName = "C:\Users\" & Environment.UserName & "\AppData\Local\Microsoft\WindowsApps\SnippingTool.exe"
                psi.UseShellExecute = True
                psi.WindowStyle = ProcessWindowStyle.Minimized
                snipProc = Process.Start(psi)
                launched = True
            Catch
                Try
                    Dim psi As New ProcessStartInfo()
                    psi.FileName = "SnippingTool.exe"
                    psi.UseShellExecute = True
                    psi.WindowStyle = ProcessWindowStyle.Minimized
                    snipProc = Process.Start(psi)
                    launched = True
                Catch
                End Try
            End Try
            If Not launched OrElse snipProc Is Nothing Then Return ""

            ' Wait, then hide any windows aggressively
            Try : snipProc.WaitForInputIdle(2000) : Catch : End Try
            HideSnippingToolRobust(1200)

            ' Keystrokes: Open → load file → Text actions → Copy all text
            Try
                My.Computer.Keyboard.SendKeys("{TAB}", True) : Thread.Sleep(100)
                My.Computer.Keyboard.SendKeys("{ENTER}", True) : Thread.Sleep(100)
                My.Computer.Keyboard.SendKeys("{ENTER}", True) : Thread.Sleep(1500)

                My.Computer.Keyboard.SendKeys(imagePath, True) : Thread.Sleep(120)
                My.Computer.Keyboard.SendKeys("{ENTER}", True) : Thread.Sleep(1300)

                My.Computer.Keyboard.SendKeys("{TAB}", True) : Thread.Sleep(120)
                My.Computer.Keyboard.SendKeys("{TAB}", True) : Thread.Sleep(120)
                My.Computer.Keyboard.SendKeys("{TAB}", True) : Thread.Sleep(120)
                My.Computer.Keyboard.SendKeys("{RIGHT}", True) : Thread.Sleep(120)
                My.Computer.Keyboard.SendKeys("{ENTER}", True) : Thread.Sleep(1600)

                My.Computer.Keyboard.SendKeys("{TAB}", True) : Thread.Sleep(120)
                My.Computer.Keyboard.SendKeys("{TAB}", True) : Thread.Sleep(120)
                My.Computer.Keyboard.SendKeys("{TAB}", True) : Thread.Sleep(120)
                My.Computer.Keyboard.SendKeys("{ENTER}", True) : Thread.Sleep(220)
            Finally
                BlockInput(False)
            End Try

            ' Clipboard can lag — try a couple times
            Dim clip As String = ""
            Dim waited As Integer = 0
            Do
                Try
                    If Clipboard.ContainsText(TextDataFormat.UnicodeText) Then
                        clip = Clipboard.GetText(TextDataFormat.UnicodeText)
                    ElseIf Clipboard.ContainsText() Then
                        clip = Clipboard.GetText()
                    End If
                Catch
                End Try
                If Not String.IsNullOrWhiteSpace(clip) Then Exit Do
                Thread.Sleep(125) : waited += 125
            Loop While waited < 2500

            ' Fallback: RTF paste
            If String.IsNullOrWhiteSpace(clip) Then
                Try
                    snipBuffer.Clear()
                    snipBuffer.Paste()
                    clip = snipBuffer.Text
                Catch
                End Try
            End If

            Return NormalizeOcrText(clip)
        Catch
            Return ""
        Finally
            ' Close the instance we launched so next run starts clean
            Try
                If snipProc IsNot Nothing AndAlso Not snipProc.HasExited Then
                    snipProc.CloseMainWindow()
                    If Not snipProc.WaitForExit(700) Then snipProc.Kill()
                End If
            Catch
            End Try
        End Try
    End Function

    ' ---- Normalization + scoring used by the fuzzy fallback ----
    Private Function NormalizeText(s As String) As String
        If s Is Nothing Then Return ""
        Dim t = s.ToUpperInvariant()
        t = New String(t.Select(Function(c) If(Char.IsLetterOrDigit(c) OrElse Char.IsWhiteSpace(c), c, " "c)).ToArray())
        t = System.Text.RegularExpressions.Regex.Replace(t, "\s+", " ").Trim()
        Return t
    End Function

    ' --- Smarter fuzzy scoring with brand gating & proximity ---
    Private Function ScoreDmmByOcr(ocrNorm As String, d As Tuple(Of String, String, String)) As Double
        Dim score As Double = 0

        ' Canonicalize
        Dim model = NormalizeText(d.Item1)      ' model name from DB
        Dim mfg = NormalizeText(d.Item2)      ' manufacturer from DB
        Dim desc = NormalizeText(d.Item3)

        ' Tokenize OCR once
        Dim words = ocrNorm.Split({" "}, StringSplitOptions.RemoveEmptyEntries)

        ' helper to find first index of token in words (exact word match)
        Dim findIndex As Func(Of String, Integer) =
        Function(tok As String) As Integer
            For i = 0 To words.Length - 1
                If words(i) = tok Then Return i
            Next
            Return -1
        End Function

        Dim idxBrand As Integer = findIndex(mfg)
        Dim idxModel As Integer = findIndex(model)

        ' Exact word matches get base points
        Dim hasBrand As Boolean = (idxBrand >= 0)
        Dim hasModel As Boolean = (idxModel >= 0)

        ' Brand found → +1
        If hasBrand Then score += 1

        ' Model found → +2, but only full credit if brand is present too
        If hasModel Then
            Dim baseModel = 2.0
            If Not hasBrand Then baseModel *= 0.35 ' heavy discount if brand missing
            score += baseModel
        End If

        ' Proximity bonus if both present: closer = higher
        If hasBrand AndAlso hasModel Then
            Dim dist = Math.Abs(idxBrand - idxModel)
            ' within 1–2 words = +1, within 3–5 = +0.6, within 6–8 = +0.3
            If dist <= 2 Then
                score += 1.0
            ElseIf dist <= 5 Then
                score += 0.6
            ElseIf dist <= 8 Then
                score += 0.3
            End If
        End If

        ' Short-model penalty when brand missing (to curb 87V-type false positives)
        If hasModel AndAlso Not hasBrand Then
            Dim isShort = (d.Item1.Length <= 4) ' raw model from DB length
            If isShort Then score -= 0.6
        End If

        ' Small description overlap bonus (same as before)
        Dim descTokens = desc.Split(" "c).Where(Function(tok) tok.Length > 3).Distinct()
        Dim overlap = descTokens.Count(Function(tok) words.Contains(tok))
        score += Math.Min(1.0, overlap * 0.1)

        ' Never return negatives
        If score < 0 Then score = 0
        Return score
    End Function

    Private Sub ShowTopMatchesInGrid(scored As List(Of Tuple(Of Double, Tuple(Of String, String, String))), topN As Integer)
        EnsureBaseGridColumns()

        ' Make sure the SCORE column is not shown anymore
        If dataGridResultDMM.Columns.Contains("SCORE") Then
            dataGridResultDMM.Columns.Remove("SCORE")
        End If

        dataGridResultDMM.Rows.Clear()

        ' Still use the scores for ordering, just don't display them
        For Each t In scored.Take(Math.Max(1, topN))
            Dim d = t.Item2   ' (Model, Manufacturer, Description)
            dataGridResultDMM.Rows.Add(d.Item1, d.Item2)
        Next
    End Sub

    ' ---- Brand + Model extraction + DB lookup + UI apply ----

    ' Pull known manufacturers from DB (used to detect brand from OCR text)
    Private Function LoadManufacturerList() As List(Of String)
        Dim list As New List(Of String)
        Try
            Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                conn.Open()
                Using cmd As New SQLiteCommand("SELECT DISTINCT manufacturer FROM dmm", conn)
                    Using r = cmd.ExecuteReader()
                        While r.Read()
                            Dim m = r("manufacturer").ToString().Trim()
                            If m <> "" Then list.Add(m)
                        End While
                    End Using
                End Using
            End Using
        Catch
        End Try
        ' Longer names first so “FLUKE NETWORKS” matches before “FLUKE”
        Return list.OrderByDescending(Function(s) s.Length).ToList()
    End Function

    Private Function Canon(ByVal s As String) As String
        Dim t = s.ToUpperInvariant()
        t = New String(t.Select(Function(c) If(Char.IsLetterOrDigit(c) OrElse Char.IsWhiteSpace(c), c, " "c)).ToArray())
        t = System.Text.RegularExpressions.Regex.Replace(t, "\s+", " ").Trim()
        Return t
    End Function

    ' --- Robust extractor: multi-word brands + proximity window + richer model regexes ---
    ' --- Robust, line-aware extractor: multi-word brands + proximity + digits-only models ---
    ' --- Line-aware extractor: multi-word brands + up-to-4-lines window + digits-only models ---
    Private Function ExtractBrandAndModelFromOcr(ocr As String, knownBrands As List(Of String)) As Tuple(Of String, String)
        ' Keep line breaks so we can use same-line / nearby-line proximity
        Dim normText = NormalizeOcrText(ocr).ToUpperInvariant()

        ' Split to lines and canonicalize each line (letters/digits/spaces)
        Dim rawLines = Regex.Split(normText, "\r?\n")
        Dim lines = rawLines.Select(Function(l) Regex.Replace(l, "[^\w\s]", " ")).
                         Select(Function(l) Regex.Replace(l, "\s+", " ").Trim()).ToList()

        ' Find best brand occurrence (longest alias wins)
        Dim bestBrandDb As String = ""
        Dim bestBrandStartTok As Integer = -1
        Dim bestBrandLine As Integer = -1
        Dim bestAliasLen As Integer = 0

        ' Build a search window of lines near the brand:
        '   same line, then next 1..4 lines (distance-scored). If no brand, search all lines.
        Dim searchOrder As New List(Of Integer)
        If bestBrandLine >= 0 Then
            searchOrder.Add(bestBrandLine)
            For off = 1 To 4
                Dim idx = bestBrandLine + off
                If idx < lines.Count Then searchOrder.Add(idx)
            Next
        Else
            For i = 0 To lines.Count - 1 : searchOrder.Add(i) : Next
        End If

        ' Model patterns:
        '   letters+digits[letters] (U1253B, DM3068, UT61E, BM869S)
        '   digits+letters          (34401A, 2000A)
        '   digits-only (2–4)       (114, 289, 87)
        Dim rx As New Regex("\b(?:[A-Z]{1,4}\d{2,6}[A-Z]{0,3}|\d{3,6}[A-Z]{1,3}|\d{2,4})\b")

        Dim bestModel As String = ""
        Dim bestScore As Double = Double.NegativeInfinity

        Dim IsAllZeros As Func(Of String, Boolean) =
        Function(s As String) Regex.IsMatch(s, "^0+$")

        For Each li In searchOrder
            Dim w = If(lines(li) = "", Array.Empty(Of String)(), lines(li).Split(" "c))
            Dim lineText = String.Join(" ", w)

            For Each m As Match In rx.Matches(lineText)
                Dim token = m.Value
                If token.Length = 0 Then Continue For
                If IsAllZeros(token) Then Continue For
                If token.Length = 1 AndAlso Char.IsDigit(token(0)) Then Continue For

                Dim digitsOnly = token.All(AddressOf Char.IsDigit)
                If digitsOnly AndAlso (token.Length < 2 OrElse token.Length > 4) Then Continue For

                ' Base by token type
                Dim score As Double = If(digitsOnly, 2.0,
                                 If(Char.IsDigit(token(0)) AndAlso Char.IsLetter(token(token.Length - 1)), 1.7, 1.5))

                ' Proximity to brand (line distance & token distance on same line)
                If bestBrandLine >= 0 Then
                    Dim lineDist = li - bestBrandLine
                    If lineDist = 0 AndAlso bestBrandStartTok >= 0 Then
                        Dim ti As Integer = Array.IndexOf(w, token)
                        If ti >= 0 Then
                            Dim dist = Math.Abs(ti - bestBrandStartTok)
                            score += If(dist <= 2, 1.2, If(dist <= 5, 0.8, 0.4))
                        Else
                            score += 0.3
                        End If
                    ElseIf lineDist > 0 Then
                        ' decay bonus the further we are from the brand line
                        score += Math.Max(0.0, 0.6 - 0.15 * (lineDist - 1))
                    End If
                End If

                ' Instrument-hint bonus
                Dim hint = rawLines(li).ToUpperInvariant()
                If hint.Contains("MULTIMETER") OrElse hint.Contains("METER") OrElse hint.Contains("TRUE RMS") Then
                    score += 0.4
                End If

                If score > bestScore Then
                    bestScore = score
                    bestModel = token
                End If
            Next

            ' If we found a candidate on the same line as the brand, that’s likely the one
            If bestBrandLine >= 0 AndAlso li = bestBrandLine AndAlso bestModel <> "" Then Exit For
        Next

        Return Tuple.Create(bestBrandDb, bestModel)
    End Function

    ' --- Canonical VB-side lookup against the already-loaded dmmItems ---
    Private Function LookupDmmInDatabase(brand As String, model As String) As Tuple(Of String, String, String)
        Dim brandC = CanonCompact(brand)
        Dim modelC = CanonCompact(model)

        If String.IsNullOrEmpty(brandC) AndAlso String.IsNullOrEmpty(modelC) Then Return Nothing

        ' Candidates by brand (use aliases too)
        Dim brandCandidates As IEnumerable(Of Tuple(Of String, String, String)) = dmmItems
        If Not String.IsNullOrEmpty(brandC) Then
            brandCandidates = dmmItems.Where(Function(d)
                                                 Dim mfgC = CanonCompact(d.Item2)
                                                 ' allow exact or prefix match on manufacturer compact form
                                                 Return mfgC = brandC OrElse mfgC.Contains(brandC) OrElse brandC.Contains(mfgC)
                                             End Function)
            If Not brandCandidates.Any() Then brandCandidates = dmmItems ' fallback
        End If

        ' 1) Exact compact model match within brand set
        If Not String.IsNullOrEmpty(modelC) Then
            Dim exact = brandCandidates.FirstOrDefault(Function(d) CanonCompact(d.Item1) = modelC)
            If exact IsNot Nothing Then Return exact
        End If

        ' 2) Contains (either side) within brand set
        If Not String.IsNullOrEmpty(modelC) Then
            Dim likeCandidate = brandCandidates.FirstOrDefault(Function(d)
                                                                   Dim mC = CanonCompact(d.Item1)
                                                                   Return mC.Contains(modelC) OrElse modelC.Contains(mC)
                                                               End Function)
            If likeCandidate IsNot Nothing Then Return likeCandidate
        End If

        ' 3) Model-only across all items
        If Not String.IsNullOrEmpty(modelC) Then
            Dim modelOnly = dmmItems.FirstOrDefault(Function(d) CanonCompact(d.Item1) = modelC)
            If modelOnly IsNot Nothing Then Return modelOnly
            Dim modelLike = dmmItems.FirstOrDefault(Function(d)
                                                        Dim mC = CanonCompact(d.Item1)
                                                        Return mC.Contains(modelC) OrElse modelC.Contains(mC)
                                                    End Function)
            If modelLike IsNot Nothing Then Return modelLike
        End If

        ' 4) Brand-only (if that’s all we have)
        If Not String.IsNullOrEmpty(brandC) Then
            Dim brandOnly = brandCandidates.FirstOrDefault()
            If brandOnly IsNot Nothing Then Return brandOnly
        End If

        Return Nothing
    End Function

    Private Sub ApplyDmmToUi(model As String, manufacturer As String, description As String)
        dmmmodel.Text = model
        manufaacturer.Text = manufacturer
        dmmdescription.Text = description

        ' Rebuild parameter lists for this model (same logic as your grid handler)
        For Each clb As CheckedListBox In {cLParamACV, cLParamDCV, cLParamACC, cLParamDCC, cLParamRES}
            clb.Items.Clear()
        Next

        Dim grouped = SQLiteHelper.LoadGroupedDMMParameters(model)
        For Each category In grouped.Keys
            Dim clb As CheckedListBox = GetCheckedListBoxForCategory(category)
            If clb Is Nothing Then Continue For

            clb.Items.Add("[" & category.ToUpper() & "]")
            clb.SetItemCheckState(clb.Items.Count - 1, CheckState.Unchecked)
            For Each rangeKey As Object In grouped(category).Keys
                clb.Items.Add("  → Range: " & rangeKey.ToString())
                For Each nominal As Object In grouped(category)(rangeKey)
                    clb.Items.Add("      → Nominal: " & nominal.ToString())
                Next
            Next
        Next
    End Sub

    ' --- Canonical "compact" form: letters+digits only, UPPERCASE (for comparisons) ---
    Private Function CanonCompact(s As String) As String
        If String.IsNullOrEmpty(s) Then Return ""
        Dim sb As New System.Text.StringBuilder(s.Length)
        For Each ch In s
            If Char.IsLetterOrDigit(ch) Then
                sb.Append(Char.ToUpperInvariant(ch))
            End If
        Next
        Return sb.ToString()
    End Function

    ' Words we strip from the *end* of manufacturer names when making brand aliases
    Private ReadOnly corpSuffix As HashSet(Of String) =
    New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "INC", "INCORPORATED", "CORP", "CORPORATION", "CO", "CO.", "COMPANY",
        "LTD", "LIMITED", "TECHNOLOGIES", "TECHNOLOGY", "INSTRUMENTS", "INSTRUMENT",
        "ELECTRONICS", "SYSTEMS", "PRODUCTS", "INTERNATIONAL"
    }

#End Region

End Class