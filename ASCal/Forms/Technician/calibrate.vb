Imports System.Data.SQLite
Imports System.Drawing.Imaging
Imports AForge.Video
Imports AForge.Video.DirectShow

Public Class calibrate

    ' ---- master switch for all temporary/testing UI & timers ----
    Private Const ENABLE_TEMP_FEATURES As Boolean = False

    Private companyDict As New Dictionary(Of String, String)

    Private dmmItems As New List(Of Tuple(Of String, String, String))
    Private dmmParametersDict As New Dictionary(Of String, List(Of String))

    Private videoSource As VideoCaptureDevice

    Dim Camera As VideoCaptureDevice
    Dim bmp As Bitmap

#Region "Navbar and Form Load"

    ' -------------------------------
    ' Handles navigation buttons (logo, logout, dashboard)
    ' -------------------------------
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

    ' -------------------------------
    ' Form Load: configure window, UI defaults, load DMMs & companies
    ' -------------------------------

    Private Sub calibrate_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Window sizing/placement
        Me.StartPosition = FormStartPosition.Manual
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)
        Me.Bounds = Screen.FromControl(Me).WorkingArea

        LoadDMMsAndParameters()

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

        dataGridResultDMM.ColumnCount = 2
        dataGridResultDMM.Columns(0).Name = "MODEL"
        dataGridResultDMM.Columns(1).Name = "MANUFACTURER"
        dataGridResultDMM.DefaultCellStyle.Font = New Font("Courier10 BT", 12)
        dataGridResultDMM.ColumnHeadersDefaultCellStyle.Font = New Font("Courier10 BT", 12)
        dataGridResultDMM.RowHeadersVisible = False

        PopulateDataGrid()
        dataGridResultDMM.ClearSelection()
        cLParamACV.Font = New Font("Courier10 BT", 14, FontStyle.Regular)
        cLParamDCV.Font = New Font("Courier10 BT", 14, FontStyle.Regular)
        cLParamACC.Font = New Font("Courier10 BT", 14, FontStyle.Regular)
        cLParamDCC.Font = New Font("Courier10 BT", 14, FontStyle.Regular)
        cLParamRES.Font = New Font("Courier10 BT", 14, FontStyle.Regular)

        '''''''''''''automatic istart

        Dim videoDevices As New FilterInfoCollection(FilterCategory.VideoInputDevice)

        ' ----- Camera init (prefers internal laptop cam) -----
        Dim cam = CreatePreferredCamera()
        If cam IsNot Nothing Then
            videoSource = cam
            AddHandler videoSource.NewFrame, AddressOf Video_NewFrame
            videoSource.Start()
        Else
            MessageBox.Show("No camera devices found.")
        End If
        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        For Each row As DataGridViewRow In dataGridResultDMM.Rows
            If Not row.IsNewRow Then
                Dim modelValue = row.Cells("MODEL").Value
                If modelValue IsNot Nothing AndAlso modelValue.ToString() = "UNI-T UT89XD" Then
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

    ' Prefer an EXTERNAL USB webcam if present; otherwise fall back gracefully
    Private Function CreatePreferredCamera() As VideoCaptureDevice
        Dim devices = New FilterInfoCollection(FilterCategory.VideoInputDevice)
        If devices Is Nothing OrElse devices.Count = 0 Then Return Nothing

        ' Heuristics: common external webcam markers (vendor/models/USB terms)
        Dim externalKeywords = New String() {
        "logi", "logitech", "brio", "c920", "c922", "c925", "c930",
        "microsoft", "lifecam", "creative", "razer", "elgato", "aver",
        "aukey", "hd pro", "usb", "webcam hd", "camera hd"
    }

        ' Heuristics: names often used by integrated laptop cameras
        ' NOTE: do NOT include generic "webcam" here (externals also say "webcam")
        Dim internalKeywords = New String() {
        "integrated", "internal", "built-in", "builtin", "laptop",
        "hd camera", "front camera"
    }

        Dim pick As FilterInfo = Nothing

        ' 1) Try to find an obvious EXTERNAL webcam
        For Each d As FilterInfo In devices
            Dim n = d.Name.ToLowerInvariant()
            If externalKeywords.Any(Function(k) n.Contains(k)) Then
                pick = d
                Exit For
            End If
        Next

        ' 2) If none matched, choose the first device that does NOT look internal
        If pick Is Nothing Then
            For Each d As FilterInfo In devices
                Dim n = d.Name.ToLowerInvariant()
                If Not internalKeywords.Any(Function(k) n.Contains(k)) Then
                    pick = d
                    Exit For
                End If
            Next
        End If

        ' 3) Last fallback: first device
        If pick Is Nothing Then pick = devices(0)

        ' Build device and choose a sensible resolution
        Dim cam = New VideoCaptureDevice(pick.MonikerString)
        Try
            Dim caps = cam.VideoCapabilities
            If caps IsNot Nothing AndAlso caps.Length > 0 Then
                ' Prefer 1280x720 if offered; else the highest resolution available
                Dim best = caps.FirstOrDefault(Function(c) c.FrameSize.Width = 1280 AndAlso c.FrameSize.Height = 720)
                If best Is Nothing Then
                    best = caps.OrderByDescending(Function(c) c.FrameSize.Width * c.FrameSize.Height).First()
                End If
                cam.VideoResolution = best
            End If
        Catch
            ' Some drivers throw; safe to ignore and use default
        End Try

        Return cam
    End Function

    ' -------------------------------
    ' Ipakita sa dataGridResult ang Top N OCR suggestions (Model, Manufacturer, Score)
    ' -------------------------------
    Private Sub ShowTopMatchesInGrid(scoredMatches As List(Of Tuple(Of Double, Tuple(Of String, String, String))),
                                 Optional topN As Integer = 3)

        ' Ensure may SCORE column (3rd col). Kung wala pa, add one.
        If Not dataGridResultDMM.Columns.Contains("SCORE") Then
            Dim scoreCol As New DataGridViewTextBoxColumn()
            scoreCol.Name = "SCORE"
            scoreCol.HeaderText = "SCORE"
            scoreCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight
            dataGridResultDMM.Columns.Add(scoreCol)
        End If

        dataGridResultDMM.Rows.Clear()

        Dim count As Integer = Math.Min(topN, scoredMatches.Count)
        For i As Integer = 0 To count - 1
            Dim item = scoredMatches(i)
            Dim dmm = item.Item2                        ' (Model, Manufacturer, Desc)
            Dim pct As String = (item.Item1 * 100).ToString("0") & "%"

            ' Rows: MODEL, MANUFACTURER, SCORE
            dataGridResultDMM.Rows.Add(dmm.Item1, dmm.Item2, pct)
        Next

        dataGridResultDMM.ClearSelection()

        ' Taglish tip para sa tech
        'MessageBox.Show("Mababa ang confidence ng OCR. Pinakita ang Top matches sa grid." &
        '            vbCrLf & "Piliin (click) ang tamang DMM row para i-autofill.",
        '            "Pick from suggestions", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Function PerformOcr(bmp As Bitmap) As String
        ' TODO: Replace with Tesseract or other OCR engine
        ' For now, return dummy text to test parsing
        Return "FLUKE 114 True RMS Multimeter"
    End Function

    ' Calculate Levenshtein distance (edit distance) between two strings
    Private Function Levenshtein(a As String, b As String) As Integer
        If String.IsNullOrEmpty(a) Then Return If(String.IsNullOrEmpty(b), 0, b.Length)
        If String.IsNullOrEmpty(b) Then Return a.Length

        Dim n = a.Length
        Dim m = b.Length
        Dim d(n, m) As Integer

        For i = 0 To n
            d(i, 0) = i
        Next
        For j = 0 To m
            d(0, j) = j
        Next

        For i = 1 To n
            For j = 1 To m
                Dim cost = If(a(i - 1) = b(j - 1), 0, 1)
                d(i, j) = Math.Min(Math.Min(d(i - 1, j) + 1, d(i, j - 1) + 1), d(i - 1, j - 1) + cost)
            Next
        Next

        Return d(n, m)
    End Function

    ' Calculate similarity ratio (0.0 – 1.0)
    Private Function Similarity(a As String, b As String) As Double
        Dim dist = Levenshtein(a.ToLower().Trim(), b.ToLower().Trim())
        Dim maxLen = Math.Max(a.Length, b.Length)
        If maxLen = 0 Then Return 1.0
        Return 1.0 - (dist / maxLen)
    End Function

    Private Sub Captured(ByVal sender As Object, ByVal EventArgs As NewFrameEventArgs)
        bmp = DirectCast(EventArgs.Frame.Clone(), Bitmap)
        PictureBox1.Image = DirectCast(EventArgs.Frame.Clone(), Bitmap)
    End Sub

    Private Sub Video_NewFrame(sender As Object, eventArgs As NewFrameEventArgs)
        ' Display the video feed in a PictureBox
        Dim bitmap As Bitmap = DirectCast(eventArgs.Frame.Clone(), Bitmap)
        PictureBox1.Image = bitmap
    End Sub

    Private Sub BtnCapture_Click(sender As Object, e As EventArgs) Handles BtnCapture.Click
        If videoSource IsNot Nothing AndAlso videoSource.IsRunning Then
            videoSource.SignalToStop()
            videoSource.WaitForStop()
        End If

        ' Gamitin ang app folder dynamically (hindi hardcoded ang username)
        Dim baseDir As String = IO.Path.Combine(My.Application.Info.DirectoryPath, "CapturedImage")
        If Not IO.Directory.Exists(baseDir) Then IO.Directory.CreateDirectory(baseDir)

        Dim capturePath As String = IO.Path.Combine(baseDir, "AAAA.jpg")
        Dim bwPath As String = IO.Path.Combine(baseDir, "BBBBB.jpg") ' (optional / currently unused)

        ' Save captured image kung may frame sa PictureBox
        If PictureBox1.Image IsNot Nothing Then
            PictureBox1.Image.Save(capturePath, ImageFormat.Jpeg)
        Else
            'kukuha ulit ng picture kasi walang laman yung picturebox1
        End If

        ' --- Single IF + Single USING lang para iwas variable shadowing / nesting issues ---
        If IO.File.Exists(capturePath) Then
            Using originalImage As Bitmap = CType(Image.FromFile(capturePath), Bitmap)

                ' Step 1: OCR raw text (palitan ng actual OCR engine kapag ready)
                Dim rawText As String = PerformOcr(originalImage)

                ' Step 2: Fuzzy match laban sa DMM database (brand + model)
                ' Taglish: I-score lahat ng DMM (brand+model) vs OCR text gamit Similarity(Levenshtein).
                '          Piliin ang best; kung mababa, ipakita sa grid ang Top suggestions.
                If dmmItems Is Nothing OrElse dmmItems.Count = 0 Then
                    MessageBox.Show("No DMM Found (dmmItems).", "DB Empty", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If

                Dim scoredMatches As New List(Of Tuple(Of Double, Tuple(Of String, String, String)))()
                For Each dmm In dmmItems
                    Dim model = dmm.Item1   ' model name galing DB
                    Dim brand = dmm.Item2   ' manufacturer/brand galing DB

                    ' Compute similarity ng buong OCR text kumpara sa brand at model
                    Dim brandScore = Similarity(rawText, brand)
                    Dim modelScore = Similarity(rawText, model)
                    Dim combined = (brandScore + modelScore) / 2   ' average score

                    scoredMatches.Add(Tuple.Create(combined, dmm))
                Next

                ' Sort by best score (desc)
                scoredMatches = scoredMatches.OrderByDescending(Function(x) x.Item1).ToList()

                Dim detectedModel As Tuple(Of String, String, String) = Nothing
                Dim bestScore As Double = scoredMatches(0).Item1
                Const CONFIDENCE_THRESHOLD As Double = 0.6   ' 60% → tweak as needed

                If bestScore >= CONFIDENCE_THRESHOLD Then
                    ' Kapag pasado sa threshold, auto-select
                    detectedModel = scoredMatches(0).Item2
                Else
                    ' Mababa ang confidence — ipakita ang top matches sa grid (no InputBox)
                    ShowTopMatchesInGrid(scoredMatches, 3)
                    ' Note: hintayin na lang ang user click; iyong existing dataGridResult_CellClick
                    ' ang bahalang mag-autofill ng dmmmodel/manufaacturer/description.
                End If

                ' Step 3: Autofill agad kung auto-detected
                If detectedModel IsNot Nothing Then
                    dmmmodel.Text = detectedModel.Item1        ' Model
                    manufaacturer.Text = detectedModel.Item2   ' Brand/Manufacturer
                    dmmdescription.Text = detectedModel.Item3  ' Description galing DB

                    MessageBox.Show(
                    $"Detected DMM: {detectedModel.Item2} {detectedModel.Item1}" & vbCrLf &
                    $"(Confidence: {bestScore:P0})",
                    "OCR Match", MessageBoxButtons.OK, MessageBoxIcon.Information
                )
                End If

            End Using
        End If
    End Sub

#End Region

#Region "Serial of prev. cert and Company Related"

    ' -------------------------------
    ' Serial number lookup:
    '   - Autofill company address
    '   - Retrieve last calibration cert & technician
    ' -------------------------------
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

    ' -------------------------------
    ' Company dropdown changes → autofill company address
    ' -------------------------------
    Private Sub contextMenuCompanies_SelectedIndexChanged(sender As Object, e As EventArgs) Handles contextMenuCompanies.SelectedIndexChanged
        Dim selectedCompany As String = contextMenuCompanies.Text.Trim()
        If companyDict.ContainsKey(selectedCompany) Then
            compAdd.Text = companyDict(selectedCompany)
        Else
            compAdd.Clear()
        End If
    End Sub

    ' -------------------------------
    ' Load active companies (name + address) from database
    ' -------------------------------
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

    ' -------------------------------
    ' Toggle On-Site vs In-House calibration (single-select)
    ' -------------------------------
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

    ' -------------------------------
    ' Load all DMM models and their parameter categories
    ' -------------------------------
    Private Sub LoadDMMsAndParameters()
        dmmItems.Clear()
        dmmParametersDict.Clear()

        Try
            Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                conn.Open()

                ' Step 1: Load all DMM Models
                Dim modelCmd As New SQLiteCommand("SELECT DISTINCT model_name, manufacturer, description FROM dmm ORDER BY model_name ASC", conn)
                Using reader As SQLiteDataReader = modelCmd.ExecuteReader()
                    While reader.Read()
                        Dim model As String = reader("model_name").ToString()
                        Dim manufacturer As String = reader("manufacturer").ToString()
                        Dim description As String = reader("description").ToString()
                        dmmItems.Add(New Tuple(Of String, String, String)(model, manufacturer, description))
                    End While
                End Using

                ' Step 2: Load parameter categories from normalized schema
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

    ' -------------------------------
    ' Load parameters for the selected DMM model
    ' -------------------------------
    Private Sub LoadParametersForSelectedDMM(model As String)
        cLParamACV.Items.Clear()

        Try
            Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                conn.Open()

                Dim query As String = "SELECT pc.name AS category, dp.id, dp.parameter_category_id " &
                                      "FROM dmm d " &
                                      "JOIN dmm_parameters dp ON d.id = dp.dmm_id " &
                                      "JOIN parameter_categories pc ON dp.parameter_category_id = pc.id " &
                                      "WHERE d.model_name = @model"

                Using cmd As New SQLiteCommand(query, conn)
                    cmd.Parameters.AddWithValue("@model", model)
                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            Dim displayText As String = reader("category").ToString()
                            cLParamACV.Items.Add(displayText)
                        End While
                    End Using
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading parameters: " & ex.Message)
        End Try
    End Sub

    ' -------------------------------
    ' DMM grid click:
    '   - Autofill model/manufacturer/description
    '   - Populate parameter checklists grouped by category
    ' -------------------------------
    Private Sub dataGridResult_CellClick(sender As Object, e As DataGridViewCellEventArgs) Handles dataGridResultDMM.CellClick
        If e.RowIndex < 0 Then Exit Sub

        Dim selectedRow As DataGridViewRow = dataGridResultDMM.Rows(e.RowIndex)
        Dim selectedModel As String = selectedRow.Cells(0).Value.ToString()

        ' Autofill DMM info
        Dim dmm = dmmItems.FirstOrDefault(Function(i) i.Item1 = selectedModel)
        If dmm Is Nothing Then
            MessageBox.Show("DMM not found in master list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        dmmmodel.Text = dmm.Item1
        manufaacturer.Text = dmm.Item2
        dmmdescription.Text = dmm.Item3

        ' Clear all parameter lists
        For Each clb As CheckedListBox In {cLParamACV, cLParamDCV, cLParamACC, cLParamDCC, cLParamRES}
            clb.Items.Clear()
        Next

        ' Load grouped parameters
        Dim grouped = SQLiteHelper.LoadGroupedDMMParameters(selectedModel)

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
        ' After picking from OCR suggestions, ibalik ang full DMM list
        If dataGridResultDMM.Columns.Contains("SCORE") Then
            dataGridResultDMM.Columns("SCORE").Visible = False
        End If
        PopulateDataGrid()

    End Sub

#End Region

#Region "Checkbox for Parameter"

    ' -------------------------------
    ' Helpers: find the correct CheckedListBox for a parameter category
    ' -------------------------------
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

    ' Populate grid (optional filter)
    Private Sub PopulateDataGrid(Optional ByVal filter As String = "")
        dataGridResultDMM.Rows.Clear()
        For Each item In dmmItems
            If filter = "" OrElse item.Item1.ToLower().Contains(filter.ToLower()) Then
                dataGridResultDMM.Rows.Add(item.Item1, item.Item2)
            End If
        Next
    End Sub

    ' Filter models as you type
    Private Sub dmmSearch_TextChanged(sender As Object, e As EventArgs)
        PopulateDataGrid(dmmSearch.Text)
    End Sub

    ' Keep legacy SelectionChanged for model autofill compatibility
    Private Sub dataGridResult_SelectionChanged(sender As Object, e As EventArgs) Handles dataGridResultDMM.SelectionChanged
        If dataGridResultDMM.SelectedRows.Count > 0 Then
            Dim selectedModel As String = dataGridResultDMM.SelectedRows(0).Cells(0).Value.ToString()
            Dim selectedItem As Tuple(Of String, String, String) = dmmItems.FirstOrDefault(Function(i) i.Item1 = selectedModel)
            If selectedItem IsNot Nothing Then
                dmmmodel.Text = selectedItem.Item1
                manufaacturer.Text = selectedItem.Item2
                dmmdescription.Text = selectedItem.Item3
            End If
        End If
    End Sub

    ' -------------------------------
    ' CheckedListBox click handler:
    '   - Toggle entire category, range, or nominal items
    ' -------------------------------
    ' This will handle the behavior for selecting or unselecting all parameters in the checked list.
    Private Sub HandleCheckedListBoxClick(clb As CheckedListBox, e As MouseEventArgs)
        ' Get the index of the clicked item
        Dim index As Integer = clb.IndexFromPoint(e.Location)
        If index < 0 Then Exit Sub ' Exit if no item was clicked

        ' Get the text of the clicked item
        Dim trimmed = clb.Items(index).ToString().TrimStart()

        ' Function to toggle item checked/unchecked
        Dim toggleChecked = Sub(i As Integer, isChecked As Boolean)
                                clb.SetItemChecked(i, isChecked)
                            End Sub

        ' Check if the item is a category
        If trimmed.StartsWith("[") Then
            ' If the category is clicked, toggle all items below it based on the category's state
            Dim isChecked = clb.GetItemChecked(index) ' Get current state without inverting
            Dim i As Integer = index + 1

            ' For each item below the category, check or uncheck based on the category state
            While i < clb.Items.Count AndAlso Not clb.Items(i).ToString().TrimStart().StartsWith("[")
                toggleChecked(i, isChecked) ' Apply the same state as the category
                i += 1
            End While

            ' You could add feedback here, e.g., change the color of the category if it is checked
        ElseIf trimmed.StartsWith("→ Range:") Then
            ' If the item is a range, toggle all associated nominal items
            Dim isChecked = clb.GetItemChecked(index)
            Dim i As Integer = index + 1

            While i < clb.Items.Count AndAlso clb.Items(i).ToString().TrimStart().StartsWith("→ Nominal:")
                toggleChecked(i, isChecked)
                i += 1
            End While
        Else
            ' Otherwise, toggle the individual item
            Dim isChecked = Not clb.GetItemChecked(index)
            toggleChecked(index, isChecked)
        End If
    End Sub

    Private Sub cLParamACV_MouseUp(sender As Object, e As MouseEventArgs) Handles cLParamACV.MouseUp
        HandleCheckedListBoxClick(cLParamACV, e)
    End Sub

    Private Sub cLParamDCV_MouseUp(sender As Object, e As MouseEventArgs) Handles cLParamDCV.MouseUp
        HandleCheckedListBoxClick(cLParamDCV, e)
    End Sub

    Private Sub cLParamACC_MouseUp(sender As Object, e As MouseEventArgs) Handles cLParamACC.MouseUp
        HandleCheckedListBoxClick(cLParamACC, e)
    End Sub

    Private Sub cLParamDCC_MouseUp(sender As Object, e As MouseEventArgs) Handles cLParamDCC.MouseUp
        HandleCheckedListBoxClick(cLParamDCC, e)
    End Sub

    Private Sub cLParamRES_MouseUp(sender As Object, e As MouseEventArgs) Handles cLParamRES.MouseUp
        HandleCheckedListBoxClick(cLParamRES, e)
    End Sub

    ' -------------------------------
    ' Select All / Unselect All parameters
    ' -------------------------------
    Private Sub btnSelectAll_Click(sender As Object, e As EventArgs) Handles btnSelectAll.Click
        For Each clb As CheckedListBox In {cLParamACV, cLParamDCV, cLParamACC, cLParamDCC, cLParamRES}
            For i As Integer = 0 To clb.Items.Count - 1
                clb.SetItemChecked(i, True)
            Next
        Next
    End Sub

    Private Sub btnUnselectAll_Click(sender As Object, e As EventArgs) Handles btnUnselectAll.Click
        For Each clb As CheckedListBox In {cLParamACV, cLParamDCV, cLParamACC, cLParamDCC, cLParamRES}
            For i As Integer = 0 To clb.Items.Count - 1
                clb.SetItemChecked(i, False)
            Next
        Next
    End Sub

#End Region

#Region "Required Fields"

    ' -------------------------------
    ' Validate required inputs:
    '   - All text fields filled
    '   - Company selected
    '   - At least one parameter checked
    ' -------------------------------
    Private Function AllInputsFilledInPanel(panel As Panel) As Boolean
        Dim excludedFields As New List(Of String) From {"dmmSearch", "specificSite", "refstand4", "DateTimePicker1", "TextBox23", "TextBox21", "TextBox19", "TextBox25", "refstand3", "refstand2", "refstand2", "refstand6", "refstand5", "refstand4", "DateTimePicker1", "TextBox31", "TextBox19", "TextBox27", "TextBox25", "TextBox28", "TextBox26", "TextBox29", "TextBox30", "TextBox20", "TextBox22", "TextBox24", "compAdd", "serialNumber", "optionsInstalled", "customerPO", "assetNumber"} 'remove all names after "compAdd"
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

        ' Company selected
        If String.IsNullOrWhiteSpace(contextMenuCompanies.Text) OrElse Not companyDict.ContainsKey(contextMenuCompanies.Text.Trim()) Then
            MessageBox.Show("Please select a valid calibration company from the list.", "Missing Company", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            contextMenuCompanies.Focus()
            Return False
        End If

        ' At least one parameter across ALL lists
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

    ' -------------------------------
    ' Start Calibration:
    '   - Gather checked parameters
    '   - Pass context (work order, company, DMM, technician, etc.)
    '   - Open calibratingResult form
    ' -------------------------------
    Private Sub btnStartCalibration_Click(sender As Object, e As EventArgs) Handles btnStartCalibration.Click
        If Not AllInputsFilledInPanel(mainPanelCalibrateInp) Then Exit Sub

        ' Build ALL checked parameters once (pass to next form)
        Dim allParams As New List(Of String)
        For Each it As Object In cLParamACV.CheckedItems : allParams.Add(it.ToString()) : Next
        For Each it As Object In cLParamDCV.CheckedItems : allParams.Add(it.ToString()) : Next
        For Each it As Object In cLParamACC.CheckedItems : allParams.Add(it.ToString()) : Next
        For Each it As Object In cLParamDCC.CheckedItems : allParams.Add(it.ToString()) : Next
        For Each it As Object In cLParamRES.CheckedItems : allParams.Add(it.ToString()) : Next

        ' Build active categories to activate on next screen
        Dim activeCategories As New List(Of String)
        If cLParamACV.CheckedItems.Count > 0 Then activeCategories.Add("AC VOLTAGE")
        If cLParamDCV.CheckedItems.Count > 0 Then activeCategories.Add("DC VOLTAGE")
        If cLParamACC.CheckedItems.Count > 0 Then activeCategories.Add("AC CURRENT")
        If cLParamDCC.CheckedItems.Count > 0 Then activeCategories.Add("DC CURRENT")
        If cLParamRES.CheckedItems.Count > 0 Then activeCategories.Add("RESISTANCE")

        ' Open the calibration entry screen and pass context (no DB save)
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
            .RefDue1 = If(refCal_DueDate1.Enabled,
              refCal_DueDate1.Value.ToString("dd-MMM-yyyy"), ""),
            .RefDesc2 = RefCal_description2.Text.Trim(),
            .RefSN2 = RefCal_serialNo2.Text.Trim(),
            .RefCalRef2 = RefCal_calReportRef2.Text.Trim(),
            .RefDue2 = If(refCal_DueDate2.Enabled,
              refCal_DueDate2.Value.ToString("dd-MMM-yyyy"), ""),
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

    ' Small table helpers
    Private Function GetTableText(table As TableLayoutPanel, col As Integer, row As Integer) As String
        Dim ctrl = table.GetControlFromPosition(col, row)
        If TypeOf ctrl Is TextBox Then
            Return DirectCast(ctrl, TextBox).Text
        End If
        Return ""
    End Function

    Private Function GetTableDate(table As TableLayoutPanel, col As Integer, row As Integer) As String
        Dim ctrl = table.GetControlFromPosition(col, row)
        If TypeOf ctrl Is DateTimePicker Then
            Return DirectCast(ctrl, DateTimePicker).Value.ToShortDateString()
        End If
        Return ""
    End Function

    Private Sub Button1_Click(sender As System.Object, e As System.EventArgs) Handles calibrateBtn.Click
        Me.Refresh()
        contextMenuCompanies.SelectedIndex = -1
        dmmSearch.Clear()
    End Sub

    Public Sub RefreshData()
        contextMenuCompanies.Items.Clear()
        companyDict.Clear()
        Dim companies = LoadAllCompanies()
        For Each comp In companies
            contextMenuCompanies.Items.Add(comp.Name)
        Next
    End Sub

    ' Find the nearest scrollable parent (Panel, etc.)
    Private Function FindScrollableParent(ctrl As Control) As ScrollableControl
        Dim p As Control = ctrl.Parent
        While p IsNot Nothing
            Dim sc = TryCast(p, ScrollableControl)
            If sc IsNot Nothing AndAlso sc.AutoScroll Then Return sc
            p = p.Parent
        End While
        Return Nothing
    End Function

    ' Forward child wheel to parent scroller
    Private Sub Child_MouseWheelScrollParent(sender As Object, e As MouseEventArgs)
        ' Stop the child (e.g., DateTimePicker) from using the wheel to change its value
        Dim he = TryCast(e, HandledMouseEventArgs)
        If he IsNot Nothing Then he.Handled = True

        Dim sc = FindScrollableParent(DirectCast(sender, Control))
        If sc Is Nothing Then Exit Sub

        ' Current logical scroll offset (note: AutoScrollPosition is negative)
        Dim curY = -sc.AutoScrollPosition.Y

        ' Wheel delta: +120 = wheel up (scroll up), -120 = wheel down
        ' Decrease Y to go up, increase Y to go down
        Dim targetY = Math.Max(0, curY - e.Delta)

        sc.AutoScrollPosition = New Point(-sc.AutoScrollPosition.X, targetY)
    End Sub

    ' Hook specific controls so hovering over them still scrolls the panel
    Private Sub HookWheelForwarding()
        Dim targets As Control() = {
        RefCal_description1, RefCal_serialNo1, RefCal_calReportRef1, refCal_DueDate1,
        RefCal_description2, RefCal_serialNo2, RefCal_calReportRef2, refCal_DueDate2
    }

        For Each c In targets
            AddHandler c.MouseWheel, AddressOf Child_MouseWheelScrollParent
        Next
    End Sub

    ' Forward child MouseWheel to the given ScrollableControl (panel)
    Private Sub ForwardWheelToPanel(sender As Object, e As MouseEventArgs, target As ScrollableControl)
        Dim he = TryCast(e, HandledMouseEventArgs)
        If he IsNot Nothing Then he.Handled = True

        If target Is Nothing Then Exit Sub

        Dim curY = -target.AutoScrollPosition.Y
        Dim targetY = Math.Max(0, curY - e.Delta)
        target.AutoScrollPosition = New Point(-target.AutoScrollPosition.X, targetY)
    End Sub

    ' Attach handlers to all children of a panel
    Private Sub HookPanelWheel(panel As ScrollableControl)
        For Each ctrl As Control In panel.Controls
            AddHandler ctrl.MouseWheel,
            Sub(s, e) ForwardWheelToPanel(s, e, panel)

            ' Also recurse into nested containers (like TableLayoutPanel)
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

End Class