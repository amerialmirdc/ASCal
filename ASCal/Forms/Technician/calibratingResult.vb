Option Strict Off

Imports System.Drawing.Imaging
Imports System.IO
Imports System.IO.Compression
Imports System.IO.Packaging
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Xml.Linq
Imports AForge.Video
Imports AForge.Video.DirectShow
Imports Drawing = System.Drawing
Imports WinForms = System.Windows.Forms

Public Class calibratingResult

#Region "Fields & Excel context"

    ' -------------------------------
    ' Handles navigation buttons (logo, logout, dashboard)
    ' -------------------------------
    Private Sub HandleNavClick(sender As Object, e As EventArgs) Handles logoBtn.Click, logoutBtn.Click, jobDashBtn.Click

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

    Private ctxDc As CalRowModule.RowContext

    ' Serial ports found on the machine
    Private myPort As String() = Array.Empty(Of String)()

    ' Parameter group holder
    Private Class ParamGroup

        ' Row-descriptor fields using the column names
        Public COL_FUNCTION As (lbl As WinForms.Label, cell As String)()

        Public RangeLabel As (lbl As WinForms.Label, cell As String)()
        Public Nominal As (lbl As WinForms.Label, cell As String)()
        Public Unit As (lbl As WinForms.Label, cell As String)()
        Public Frequency As (lbl As WinForms.Label, cell As String)()
        Public FreqUnit As (lbl As WinForms.Label, cell As String)()

        Public MV1 As (tb As WinForms.TextBox, cell As String)()
        Public MV2 As (tb As WinForms.TextBox, cell As String)()
        Public MV3 As (tb As WinForms.TextBox, cell As String)()
        Public Average As (lbl As WinForms.Label, cell As String)()
        Public [Error] As (lbl As WinForms.Label, cell As String)()
        Public FinalUncDecl As (lbl As WinForms.Label, cell As String)()
        Public Tolerance As (tb As WinForms.TextBox, cell As String)()
        Public UpperLimit As (tb As WinForms.TextBox, cell As String)()
        Public LowerLimit As (tb As WinForms.TextBox, cell As String)()
        Public Remarks As (tb As WinForms.TextBox, cell As String)()

        Public TemplateRowCount As Integer   ' how many rows the sheet actually has (by Column A scan)

        ' Identity / grouping
        Public SheetKey As String      ' actual dictionary key (e.g., "ACV", "ACV_1")

        Public SheetBase As String     ' base name without numeric suffix (e.g., "ACV")
        Public SheetIndex As Integer   ' 0 for first, 1 for second, etc.
        Public SheetId As String       ' e.g., "acv:0" – stable, easy to group

    End Class

    Private Groups As New Dictionary(Of String, ParamGroup)(StringComparer.OrdinalIgnoreCase)
    ' ========= GROUP STORAGE =========

    Private currentGroup As ParamGroup = Nothing
    Private currentRowIdx As Integer = -1
    Private currentExcelRow As Integer = -1
    Private reportGenerated As Boolean = False

#End Region

#Region "Inbound properties from calibrate.vb (headers & context)"

    Public Property JobId As Integer
    Public Property WorkOrderNumber As String
    Public Property CompanyName As String
    Public Property CompanyAddress As String
    Public Property Model As String
    Public Property Manufacturer As String
    Public Property Description As String
    Public Property TechnicianInitials As String
    Public Property TechnicianName As String
    Public Property CalibrationType As String
    Public Property SpecificSite As String
    Public Property SerialNumber As String
    Public Property SelectedParameters As List(Of String)
    Public Property ActiveCategories As List(Of String)

    ' Left header details
    Public Property Range As String                  ' K17

    Public Property Readability As String            ' K19
    Public Property PrevSesCalCert As String         ' K21

    ' Right-side header block
    Public Property ReceivedDate As String           ' AL9

    Public Property CalibrationDate As String        ' AL11
    Public Property OptionsInstalled As String       ' AL13
    Public Property CustomerPO As String             ' AL15
    Public Property AssetNumber As String            ' AL17
    Public Property AccuracyHeader As String         ' AL19
    Public Property PreviousTechnician As String     ' AL21

    ' Environmental conditions
    Public Property TempStart As String              ' K41

    Public Property TempEnd As String                ' K42
    Public Property HumidityStart As String          ' T41
    Public Property HumidityEnd As String            ' T42

    ' Optional: reference / accessory fields
    Public Property RefDesc1 As String

    Public Property RefSN1 As String
    Public Property RefCalRef1 As String
    Public Property RefDue1 As String
    Public Property RefDesc2 As String
    Public Property RefSN2 As String
    Public Property RefCalRef2 As String
    Public Property RefDue2 As String

    Public Property AccDesc1 As String
    Public Property AccSN1 As String
    Public Property AccCalBrand1 As String
    Public Property AccModel1 As String
    Public Property AccDesc2 As String
    Public Property AccSN2 As String
    Public Property AccCalBrand2 As String
    Public Property AccModel2 As String
    Public Property calMathod As String

#End Region

#Region "Core compute + OCR hooks + TEMP stubs"

    Private Function ReorderTupleTB(arr As (tb As TextBox, cell As String)(), order As Integer()) _
        As (tb As TextBox, cell As String)()
        If arr Is Nothing Then Return Nothing
        Dim out(arr.Length - 1) As (TextBox, String)
        For i = 0 To Math.Min(arr.Length, order.Length) - 1 : out(i) = arr(order(i)) : Next
        Return out
    End Function

    Private Function ReorderTupleLBL(arr As (lbl As Label, cell As String)(), order As Integer()) _
    As (lbl As Label, cell As String)()
        If arr Is Nothing Then Return Nothing
        Dim out(arr.Length - 1) As (Label, String)
        For i = 0 To Math.Min(arr.Length, order.Length) - 1
            out(i) = arr(order(i))
        Next
        Return out
    End Function

    Private Function GetMappedLabel(arr As (lbl As Label, cell As String)(), idx As Integer) As Label
        If arr Is Nothing OrElse idx < 0 OrElse idx >= arr.Length Then Return Nothing
        Return arr(idx).lbl
    End Function

    ' Pick the best “anchor” control for a given row by scanning ALL columns
    Private Function FirstControlOfRow(g As ParamGroup, i As Integer) As WinForms.Control
        Dim best As WinForms.Control = Nothing
        Dim bestTop As Integer = Integer.MaxValue

        Dim tryCtrl = Sub(c As WinForms.Control)
                          If c IsNot Nothing AndAlso c.Top < bestTop Then
                              best = c : bestTop = c.Top
                          End If
                      End Sub

        ' Descriptor labels (left side) – new
        If g.RangeLabel IsNot Nothing AndAlso i < g.RangeLabel.Length Then tryCtrl(g.RangeLabel(i).lbl)
        If g.COL_FUNCTION IsNot Nothing AndAlso i < g.COL_FUNCTION.Length Then tryCtrl(g.COL_FUNCTION(i).lbl)
        If g.Nominal IsNot Nothing AndAlso i < g.Nominal.Length Then tryCtrl(g.Nominal(i).lbl)
        If g.Unit IsNot Nothing AndAlso i < g.Unit.Length Then tryCtrl(g.Unit(i).lbl)
        If g.Frequency IsNot Nothing AndAlso i < g.Frequency.Length Then tryCtrl(g.Frequency(i).lbl)
        If g.FreqUnit IsNot Nothing AndAlso i < g.FreqUnit.Length Then tryCtrl(g.FreqUnit(i).lbl)

        ' Inputs
        If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length Then tryCtrl(g.MV1(i).tb)
        If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length Then tryCtrl(g.MV2(i).tb)
        If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length Then tryCtrl(g.MV3(i).tb)

        ' Result labels
        If g.Average IsNot Nothing AndAlso i < g.Average.Length Then tryCtrl(g.Average(i).lbl)
        If g.Error IsNot Nothing AndAlso i < g.Error.Length Then tryCtrl(g.Error(i).lbl)
        If g.FinalUncDecl IsNot Nothing AndAlso i < g.FinalUncDecl.Length Then tryCtrl(g.FinalUncDecl(i).lbl)

        ' Limits / remarks
        If g.Tolerance IsNot Nothing AndAlso i < g.Tolerance.Length Then tryCtrl(g.Tolerance(i).tb)
        If g.UpperLimit IsNot Nothing AndAlso i < g.UpperLimit.Length Then tryCtrl(g.UpperLimit(i).tb)
        If g.LowerLimit IsNot Nothing AndAlso i < g.LowerLimit.Length Then tryCtrl(g.LowerLimit(i).tb)
        If g.Remarks IsNot Nothing AndAlso i < g.Remarks.Length Then tryCtrl(g.Remarks(i).tb)

        Return best ' may be Nothing (caller handles)
    End Function

    Private Sub NormalizeGroupOrderByTop(g As ParamGroup)
        If g Is Nothing Then Exit Sub

        ' Define rows by inputs (prefer MV1)
        Dim n As Integer = 0
        If g.MV1 IsNot Nothing Then n = Math.Max(n, g.MV1.Length)
        If g.MV2 IsNot Nothing Then n = Math.Max(n, g.MV2.Length)
        If g.MV3 IsNot Nothing Then n = Math.Max(n, g.MV3.Length)
        If n <= 1 Then Exit Sub

        ' Order by MV1.Top; if missing, fall back to MV2→MV3→FirstControlOfRow
        Dim pairs As New List(Of Tuple(Of Integer, Integer))()
        For i = 0 To n - 1
            Dim topVal = Integer.MaxValue
            If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length AndAlso g.MV1(i).tb IsNot Nothing Then
                topVal = g.MV1(i).tb.Top
            ElseIf g.MV2 IsNot Nothing AndAlso i < g.MV2.Length AndAlso g.MV2(i).tb IsNot Nothing Then
                topVal = g.MV2(i).tb.Top
            ElseIf g.MV3 IsNot Nothing AndAlso i < g.MV3.Length AndAlso g.MV3(i).tb IsNot Nothing Then
                topVal = g.MV3(i).tb.Top
            Else
                Dim c = FirstControlOfRow(g, i)
                If c IsNot Nothing Then topVal = c.Top
            End If
            pairs.Add(Tuple.Create(i, topVal))
        Next
        Dim order = pairs.OrderBy(Function(t) t.Item2).Select(Function(t) t.Item1).ToArray()

        ' Reorder inputs/outputs/labels together
        g.MV1 = ReorderTupleTB(g.MV1, order)
        g.MV2 = ReorderTupleTB(g.MV2, order)
        g.MV3 = ReorderTupleTB(g.MV3, order)
        g.Average = ReorderTupleLBL(g.Average, order)
        g.[Error] = ReorderTupleLBL(g.[Error], order)
        g.FinalUncDecl = ReorderTupleLBL(g.FinalUncDecl, order)
        g.Tolerance = ReorderTupleTB(g.Tolerance, order)
        g.UpperLimit = ReorderTupleTB(g.UpperLimit, order)
        g.LowerLimit = ReorderTupleTB(g.LowerLimit, order)
        g.Remarks = ReorderTupleTB(g.Remarks, order)

        ' Reorder new left labels (tuple)
        g.COL_FUNCTION = ReorderTupleLBL(g.COL_FUNCTION, order)
        g.RangeLabel = ReorderTupleLBL(g.RangeLabel, order)
        g.Nominal = ReorderTupleLBL(g.Nominal, order)
        g.Unit = ReorderTupleLBL(g.Unit, order)
        g.Frequency = ReorderTupleLBL(g.Frequency, order)
        g.FreqUnit = ReorderTupleLBL(g.FreqUnit, order)
    End Sub

    ' Accepts 1..N row indices. Single-row → compute now; multi-row → timer.
    ' Kick compute for exactly one row and rearm the tick to finish the UI update.
    Private Sub StartRowCompute(g As ParamGroup, rowIdx As Integer)
        If g Is Nothing OrElse rowIdx < 0 Then Exit Sub

        ' Resolve Excel row from any MV* mapped address for this row
        Dim addr As String = Nothing
        If g.MV3 IsNot Nothing AndAlso rowIdx < g.MV3.Length Then addr = g.MV3(rowIdx).cell
        If String.IsNullOrWhiteSpace(addr) AndAlso g.MV2 IsNot Nothing AndAlso rowIdx < g.MV2.Length Then addr = g.MV2(rowIdx).cell
        If String.IsNullOrWhiteSpace(addr) AndAlso g.MV1 IsNot Nothing AndAlso rowIdx < g.MV1.Length Then addr = g.MV1(rowIdx).cell
        If String.IsNullOrWhiteSpace(addr) Then Exit Sub

        Dim excelRow As Integer = GetRowFromAddr(addr)
        If excelRow <= 0 Then Exit Sub

        Try : SetSheetsIfChanged(ctxDc, g.SheetKey) : Catch : End Try
        ctxDc.TargetRow = excelRow

        ' Wire Pre/After for this row and compute now
        ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, g, rowIdx)
        ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, g, rowIdx)
        CalRowModule.RecalculateNow(ctxDc)
    End Sub

    ' Add inside calibratingResult (e.g., near ApplyCategoriesAndSelection)
    Private Sub SetLabelTextIfPresent(lb As Label, value As String)
        If lb Is Nothing Then Exit Sub
        lb.Text = value
        lb.Visible = True
    End Sub

    ' Convenience: use the *current* row and fill the first empty MV cell
    Private Sub AutoApplyReadingToCurrentRow(reading As String)
        If String.IsNullOrWhiteSpace(reading) Then Exit Sub

        ' Safety: mappings must exist

        ' Pick group if not set yet (use parameter + optional DMMmode)
        If currentGroup Is Nothing Then
            Dim p As String = If(DMMtxtparameter.Text, "").Trim().ToUpperInvariant()   ' "V", "A", "Ω"/"OHM"
            Dim mode As String = ""
            Dim mCtrl = Me.Controls.Find("DMMmode", True).FirstOrDefault()
            If TypeOf mCtrl Is TextBox Then mode = DirectCast(mCtrl, TextBox).Text.Trim().ToUpperInvariant()   ' "AC" or "DC"

            Select Case p
                Case Else
                    ' Fallback: first visible group that has MV1 rows
                    For Each g In Groups.Values
                        If g IsNot Nothing AndAlso g.MV1 IsNot Nothing Then
                            For i = 0 To g.MV1.Length - 1
                                If g.MV1(i).tb IsNot Nothing AndAlso g.MV1(i).tb.Visible Then
                                    currentGroup = g : currentRowIdx = i : Exit For
                                End If
                            Next
                        End If
                        If currentGroup IsNot Nothing Then Exit For
                    Next
            End Select
        End If
        If currentGroup Is Nothing Then Exit Sub

        ' Choose row if none yet (first visible)
        If currentRowIdx < 0 Then
            currentRowIdx = 0
            If currentGroup.MV1 IsNot Nothing Then
                For i = 0 To currentGroup.MV1.Length - 1
                    Dim tb = currentGroup.MV1(i).tb
                    If tb IsNot Nothing AndAlso tb.Visible Then currentRowIdx = i : Exit For
                Next
            End If
        End If

        ' --- Write to the first empty MV in the row ---
        Dim wrote As Boolean = False
        If currentGroup.MV1 IsNot Nothing AndAlso currentRowIdx < currentGroup.MV1.Length Then
            Dim tb = currentGroup.MV1(currentRowIdx).tb
            ' Remove the IsEmptyMv function calls and handle them directly:
            If tb IsNot Nothing AndAlso String.IsNullOrWhiteSpace(tb.Text) Then
                tb.Text = reading
                wrote = True
            End If
        End If
        If Not wrote AndAlso currentGroup.MV2 IsNot Nothing AndAlso currentRowIdx < currentGroup.MV2.Length Then
            Dim tb = currentGroup.MV2(currentRowIdx).tb
            ' Remove the IsEmptyMv function calls and handle them directly:
            If tb IsNot Nothing AndAlso String.IsNullOrWhiteSpace(tb.Text) Then
                tb.Text = reading
                wrote = True
            End If
        End If
        If Not wrote AndAlso currentGroup.MV3 IsNot Nothing AndAlso currentRowIdx < currentGroup.MV3.Length Then
            Dim tb = currentGroup.MV3(currentRowIdx).tb
            ' Remove the IsEmptyMv function calls and handle them directly:
            If tb IsNot Nothing AndAlso String.IsNullOrWhiteSpace(tb.Text) Then
                tb.Text = reading
                wrote = True
            End If
        End If

        ' If row complete → compute → advance pointer to next visible row
        If IsRowComplete(currentGroup, currentRowIdx) Then
            currentExcelRow = GetRowFromAddr(currentGroup.MV3(currentRowIdx).cell)
            ctxDc.TargetRow = currentExcelRow
            StartRowCompute(currentGroup, currentRowIdx)

            ' Find the next visible row
            If currentGroup.MV1 IsNot Nothing Then
                For i = currentRowIdx + 1 To currentGroup.MV1.Length - 1
                    Dim tb = currentGroup.MV1(i).tb
                    If tb IsNot Nothing AndAlso tb.Visible Then
                        currentRowIdx = i
                        ' Move focus to the first visible textbox in the next row (MV1)
                        Dim nextControl = currentGroup.MV1(currentRowIdx).tb
                        If nextControl IsNot Nothing Then
                            nextControl.Focus()
                            nextControl.SelectAll()
                        End If
                        Exit Sub
                    End If
                Next
            End If
        End If
    End Sub

    ' --- needed because TEMP regions are commented out ---
    ' keeps bulk textbox updates from re-triggering compute
    Private isBulkUpdating As Boolean = False

    ' nominal sequencer targets placeholder (ApplyActiveCategories still touches this)
    Private nomSeqTargets As List(Of (tb As TextBox, value As String)) = Nothing

    ' overall run stopwatch (used by OnDcComputeTimerTick timing summary)
    'Private runStopwatch As System.Diagnostics.Stopwatch = Nothing

#End Region

#Region "Load / Close"

    Public Property UseSerialUI As Boolean = True

    Private Sub calibratingResult_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' ================= WINDOW =================
        Me.StartPosition = FormStartPosition.Manual
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)
        Me.Bounds = Screen.FromControl(Me).WorkingArea

        ' ================= SERIAL UI =================
        If UseSerialUI Then
            myPort = IO.Ports.SerialPort.GetPortNames()
            CmbBaud.Items.Clear()
            CmbBaud.Items.AddRange(New Object() {9600, 19200, 38400, 57600, 115200})
            If CmbBaud.Items.Count > 0 Then CmbBaud.SelectedIndex = 0
            If myPort IsNot Nothing AndAlso myPort.Length > 0 Then
                CmbPort.Items.AddRange(myPort)
                CmbPort.SelectedIndex = 0
            End If
            BtnDisconnect.Enabled = False
        Else
            Try
                If SerialPort1 IsNot Nothing AndAlso SerialPort1.IsOpen Then SerialPort1.Close()
            Catch
            End Try
            For Each c As WinForms.Control In New WinForms.Control() {CmbPort, CmbBaud, BtnConnect, BtnDisconnect, Label633, Label634}
                If c IsNot Nothing Then c.Visible = False
            Next
        End If

        ' ================= CAMERA =================
        Try
            If videoSource IsNot Nothing Then
                RemoveHandler videoSource.NewFrame, AddressOf Video_NewFrame
                If videoSource.IsRunning Then videoSource.SignalToStop()
            End If
        Catch
        End Try
        Dim cam = CreatePreferredCamera()
        If cam IsNot Nothing Then
            videoSource = cam
            AddHandler videoSource.NewFrame, AddressOf Video_NewFrame
            videoSource.Start()
            ' keep camera visible above preview (assumes your PictureBox name; adjust if different)
            Dim camCtl = Me.Controls.Find("pbCamera", True).FirstOrDefault()
            If camCtl IsNot Nothing Then
                camCtl.Visible = True
                camCtl.BringToFront()
            End If
        End If

        ' ================= TEMPLATE =================
        Dim appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        Dim tmplDir = IO.Path.Combine(appData, "DMMCal", "Templates")
        If Not IO.Directory.Exists(tmplDir) Then IO.Directory.CreateDirectory(tmplDir)

        Dim Slug = Function(s As String) As String
                       s = If(s, "").Trim()
                       If s.Length = 0 Then Return "NA"
                       For Each ch In IO.Path.GetInvalidFileNameChars()
                           s = s.Replace(ch, "_"c)
                       Next
                       Return s
                   End Function

        Dim m = If(Model, "").Trim()
        Dim mf = If(Manufacturer, "").Trim()
        Dim ds = If(Description, "").Trim()
        Dim p3 = IO.Path.Combine(tmplDir, $"{Slug(m)}__{Slug(mf)}__{Slug(ds)}.xlsx")
        Dim p2 = IO.Path.Combine(tmplDir, $"{Slug(m)}__{Slug(mf)}.xlsx")
        Dim p1 = IO.Path.Combine(tmplDir, $"{Slug(m)}.xlsx")

        Dim template As String = ""
        If IO.File.Exists(p3) Then
            template = p3
        ElseIf IO.File.Exists(p2) Then
            template = p2
        ElseIf IO.File.Exists(p1) Then
            template = p1
        End If

        If String.IsNullOrWhiteSpace(template) OrElse Not IO.File.Exists(template) Then
            MessageBox.Show("Missing Excel template for this model." & Environment.NewLine &
                    $"Looked in: {tmplDir}", "Template Not Found",
                    MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Dim unique = $"{DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{Guid.NewGuid:N}"
        Dim workingCopy = IO.Path.Combine(IO.Path.GetTempPath(),
    $"ASCal_{NormalizeFile(WorkOrderNumber)}_{NormalizeFile(SerialNumber)}_{unique}.xlsx")

        Try
            If IO.File.Exists(workingCopy) Then IO.File.Delete(workingCopy)
            If IO.File.Exists(workingCopy) Then IO.File.Delete(workingCopy)
            IO.File.Copy(template, workingCopy, True)
        Catch ex As Exception
            MessageBox.Show("Unable to prepare Excel template: " & ex.Message, "Template Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End Try

        ' Excel context (keep this)
        ctxDc = New CalRowModule.RowContext With {
        .TemplatePath = workingCopy,
        .SheetInputsName = "DataSheet",
        .SheetFormulaName = "DataSheet",
        .hostControls = Me.Controls}

        CalRowModule.Initialize(ctxDc)
        If ctxDc Is Nothing Then
            MessageBox.Show("Failed to initialize Excel context.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If
        ' ====== SHEET DISCOVERY & MAPPING (include ALL sheets) ======
        'Groups.Clear()

        Dim allSheets = GetWorksheetNamesFromXlsx(ctxDc.TemplatePath)
        If allSheets Is Nothing OrElse allSheets.Count = 0 Then
            allSheets = New List(Of String) From {ctxDc.SheetInputsName}
        End If

        Dim dataSheets As New List(Of String)

        CalRowModule.WithWorksheet(ctxDc, Sub(ws)
                                              For Each sh In allSheets
                                                  Dim looksData As Boolean = False
                                                  Try
                                                      ctxDc.SheetInputsName = sh
                                                      ctxDc.SheetFormulaName = sh

                                                      Dim blankStreak As Integer = 0
                                                      For r As Integer = 2 To 50
                                                          Dim aVal As String = CalRowModule.ReadCell(ws, "A" & r)
                                                          Dim cVal As String = CalRowModule.ReadCell(ws, "C" & r)
                                                          If String.IsNullOrWhiteSpace(aVal) AndAlso String.IsNullOrWhiteSpace(cVal) Then
                                                              blankStreak += 1
                                                              If blankStreak >= 20 Then Exit For
                                                          Else
                                                              looksData = True
                                                              Exit For
                                                          End If
                                                      Next
                                                  Catch
                                                      looksData = False
                                                  End Try
                                                  If looksData Then dataSheets.Add(sh)
                                              Next
                                          End Sub)

        If dataSheets.Count = 0 Then dataSheets.Add(allSheets(0))  ' fallback

        ' ================= POPULATE namesArr =================
        Dim namesArr As New List(Of String)()
        CalRowModule.WithWorksheet(ctxDc, Sub(ws)
                                              ' Assume we are reading from column A
                                              Dim row As Integer = 2 ' Starting from row 2
                                              While True
                                                  Dim aVal As String = CalRowModule.ReadCell(ws, "A" & row.ToString())

                                                  ' Debugging: Log value to debug output to ensure the value is being read
                                                  Debug.WriteLine($"Reading row {row}: {aVal}")

                                                  If String.IsNullOrWhiteSpace(aVal) Then
                                                      Exit While ' Stop if a blank cell is found
                                                  End If
                                                  namesArr.Add(aVal) ' Add the value to namesArr

                                                  '' Show the value being populated on UI thread
                                                  'If Me.InvokeRequired Then
                                                  '    Me.Invoke(Sub()
                                                  '                  MessageBox.Show($"Populating name: {aVal}", "Populating Names", MessageBoxButtons.OK, MessageBoxIcon.Information)
                                                  '              End Sub)
                                                  'Else
                                                  '    MessageBox.Show($"Populating name: {aVal}", "Populating Names", MessageBoxButtons.OK, MessageBoxIcon.Information)
                                                  'End If

                                                  row += 1
                                              End While
                                          End Sub)

        ' Convert namesArr to an array
        Dim namesArrArray As String() = namesArr.ToArray()

        ' Continue with the rest of your logic
        For Each sh In allSheets
            ctxDc.SheetInputsName = sh
            ctxDc.SheetFormulaName = sh
            Me.InitMappings()
        Next

        ctxDc.SheetInputsName = dataSheets(0)
        ctxDc.SheetFormulaName = dataSheets(0)

        If Groups Is Nothing Then Groups = New Dictionary(Of String, ParamGroup)(StringComparer.OrdinalIgnoreCase)
        If Groups.Count = 0 Then
            MessageBox.Show("No parameter groups were mapped by InitMappings()." & Environment.NewLine &
            "Ensure InitMappings fills Groups(<any key you want>) directly.",
            "Mappings Missing", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        For Each g In Groups.Values
            NormalizeGroupOrderByTop(g)
        Next

        ' ================= PREVIEW PANEL (populate) =================
        PopulatePreview()

        ApplyCategoriesAndSelection()

        HookLiveCompute()
    End Sub

    ' --- Render preview UI into a specific FlowLayoutPanel ---
    Private Sub PopulatePreview(Optional target As FlowLayoutPanel = Nothing)

        ' ---------- Resolve target ----------
        Dim fl As FlowLayoutPanel = target
        If fl Is Nothing Then
            fl = TryCast(Me.Controls.Find("previewcalibrating", True).FirstOrDefault(), FlowLayoutPanel)
            If fl Is Nothing Then Exit Sub
            fl.Visible = True
        End If

        fl.SuspendLayout()
        fl.FlowDirection = FlowDirection.TopDown
        fl.WrapContents = False
        Dim oldScroll = fl.AutoScroll
        fl.AutoScroll = False
        fl.Controls.Clear()

        ' ---------- Available width ----------
        Dim availW As Integer = fl.DisplayRectangle.Width
        If availW <= 0 Then availW = If(fl.Parent IsNot Nothing, fl.Parent.ClientSize.Width, 800)
        availW = Math.Max(200, availW - fl.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth)

        ' ---------- Column maps (case-insensitive) ----------
        Dim colLeft As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        Dim colWidth As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

        Dim GetW As Func(Of String, Integer) =
        Function(k As String)
            Dim v As Integer
            If colWidth.TryGetValue(k, v) Then Return v
            Return 80
        End Function

        Dim GetL As Func(Of String, Integer) =
        Function(k As String)
            Dim v As Integer
            If colLeft.TryGetValue(k, v) Then Return v
            Return 0
        End Function

        ' ---------- Header panel ----------
        Dim hdr As New Panel() With {.Height = 22, .Width = availW}
        Dim x As Integer = 0

        Dim addHdr =
        Sub(text As String, left As Integer, width As Integer)
            Dim lbl As New Label() With {
                .AutoSize = False, .Text = text, .Left = left, .Top = 2,
                .Width = width, .Font = New Font(Me.Font, FontStyle.Bold)
            }
            hdr.Controls.Add(lbl)
        End Sub

        Dim addColDynamic =
        Sub(key As String, values As IEnumerable(Of String), minW As Integer, pad As Integer)
            Dim seq As IEnumerable(Of String) = If(values, Enumerable.Empty(Of String)())
            Dim maxWidth As Integer =
                seq.Select(Function(v) If(v IsNot Nothing, TextRenderer.MeasureText(v, Me.Font).Width, 0)).
                    DefaultIfEmpty(0).Max()
            Dim w As Integer = Math.Max(minW, maxWidth + pad)
            addHdr(key, x, w)
            colLeft(key) = x
            colWidth(key) = w
            x += w
        End Sub

        Dim addColFixed =
        Sub(key As String, width As Integer)
            addHdr(key, x, width)
            colLeft(key) = x
            colWidth(key) = width
            x += width
        End Sub

        ' ---------- Use insertion order (no sorting) ----------
        Dim ordered As List(Of ParamGroup) = Groups.Values.ToList()

        ' ---------- Build columns from the FIRST available group ----------
        For Each g In ordered
            If g Is Nothing Then Continue For
            addColDynamic("Function", If(g.COL_FUNCTION IsNot Nothing, g.COL_FUNCTION.Select(Function(t) If(t.lbl Is Nothing, "", t.lbl.Text)), Nothing), 60, 10)
            addColDynamic("RangeLabel", If(g.RangeLabel IsNot Nothing, g.RangeLabel.Select(Function(t) If(t.lbl Is Nothing, "", t.lbl.Text)), Nothing), 60, 10)
            addColDynamic("Nominal", If(g.Nominal IsNot Nothing, g.Nominal.Select(Function(t) If(t.lbl Is Nothing, "", t.lbl.Text)), Nothing), 60, 10)
            addColDynamic("Unit", If(g.Unit IsNot Nothing, g.Unit.Select(Function(t) If(t.lbl Is Nothing, "", t.lbl.Text)), Nothing), 60, 10)
            addColDynamic("Frequency", If(g.Frequency IsNot Nothing, g.Frequency.Select(Function(t) If(t.lbl Is Nothing, "", t.lbl.Text)), Nothing), 60, 10)
            addColDynamic("FreqUnit", If(g.FreqUnit IsNot Nothing, g.FreqUnit.Select(Function(t) If(t.lbl Is Nothing, "", t.lbl.Text)), Nothing), 60, 10)
            Exit For
        Next

        ' Fixed-width inputs/results columns
        addColFixed("MV1", 85) : addColFixed("MV2", 85) : addColFixed("MV3", 85)
        addColFixed("Average", 85) : addColFixed("Error", 85)
        addColFixed("Tolerance", 85) : addColFixed("UpperLimit", 85) : addColFixed("LowerLimit", 85)
        addColFixed("Remarks", 140) : addColFixed("Final_U", 85)

        fl.Controls.Add(hdr)
        fl.SetFlowBreak(hdr, True)

        ' ---------- Per-row placers (prefix names with SHEET NAME = SheetKey) ----------
        Dim placeLblTuple =
        Sub(ByRef tup As (Label, String), key As String, value As String, pg As ParamGroup, i As Integer, rowPanel As Panel)
            Dim sheetName As String = pg.SheetKey   ' actual worksheet/tab name
            If tup.Item1 Is Nothing Then
                tup.Item1 = New Label() With {.Name = $"{sheetName}__{key}_{i}", .AutoSize = False, .Height = 20}
            Else
                tup.Item1.Name = $"{sheetName}__{key}_{i}"
            End If
            If tup.Item1.Parent IsNot rowPanel Then tup.Item1.Parent = rowPanel
            tup.Item1.Text = value
            tup.Item1.Visible = True
            tup.Item1.Left = GetL(key)
            tup.Item1.Top = 4
            tup.Item1.Width = GetW(key)
        End Sub

        Dim placeTbTuple =
        Sub(ByRef tup As (TextBox, String), key As String, pg As ParamGroup, baseName As String, i As Integer, rowPanel As Panel)
            Dim sheetName As String = pg.SheetKey
            If tup.Item1 Is Nothing Then
                tup.Item1 = New TextBox() With {.Name = $"{sheetName}__{baseName}_{i}"}
            Else
                tup.Item1.Name = $"{sheetName}__{baseName}_{i}"
            End If
            If tup.Item1.Parent IsNot rowPanel Then tup.Item1.Parent = rowPanel
            tup.Item1.Visible = True
            tup.Item1.Width = Math.Max(0, GetW(key) - 6)
            tup.Item1.Left = GetL(key) + 3
            tup.Item1.Top = 2
        End Sub

        Dim placeLbTuple2 =
        Sub(ByRef tup As (Label, String), key As String, pg As ParamGroup, baseName As String, i As Integer, rowPanel As Panel)
            Dim sheetName As String = pg.SheetKey
            If tup.Item1 Is Nothing Then
                tup.Item1 = New Label() With {.Name = $"{sheetName}__{baseName}_{i}", .AutoSize = False, .Height = 20}
            Else
                tup.Item1.Name = $"{sheetName}__{baseName}_{i}"
            End If
            If tup.Item1.Parent IsNot rowPanel Then tup.Item1.Parent = rowPanel
            tup.Item1.Visible = True
            tup.Item1.Left = GetL(key)
            tup.Item1.Top = 4
            tup.Item1.Width = GetW(key)
        End Sub

        ' ---------- Safe getters (bounds-checked) ----------
        Dim GetLblText As Func(Of (Label, String)(), Integer, String) =
        Function(arr As (Label, String)(), idx As Integer) As String
            If arr Is Nothing OrElse idx < 0 OrElse idx >= arr.Length Then Return ""
            Dim lb = arr(idx).Item1
            Return If(lb Is Nothing, "", lb.Text)
        End Function

        ' ---------- Row builder ----------
        Dim addRow =
        Sub(g As ParamGroup, i As Integer)
            If g Is Nothing Then Exit Sub

            Dim rowPanel As New Panel() With {
                .Margin = New Padding(0, 0, 0, 6),
                .Padding = New Padding(0),
                .BorderStyle = BorderStyle.FixedSingle,
                .Width = availW,
                .Height = 26,
                .BackColor = Color.FromArgb(248, 248, 248)
            }

            Dim rngTxt As String = GetLblText(g.RangeLabel, i)
            Dim fnTxt As String = GetLblText(g.COL_FUNCTION, i)
            Dim nomTxt As String = GetLblText(g.Nominal, i)
            Dim untTxt As String = GetLblText(g.Unit, i)
            Dim frqTxt As String = GetLblText(g.Frequency, i)
            Dim fuTxt As String = GetLblText(g.FreqUnit, i)

            ' --- LEFT LABELS (never reuse last element) ---
            If g.COL_FUNCTION IsNot Nothing AndAlso i < g.COL_FUNCTION.Length Then
                placeLblTuple(g.COL_FUNCTION(i), "Function", fnTxt, g, i, rowPanel)
            Else
                Dim tmp As (Label, String) = (Nothing, Nothing)
                placeLblTuple(tmp, "Function", fnTxt, g, i, rowPanel)
            End If

            If g.RangeLabel IsNot Nothing AndAlso i < g.RangeLabel.Length Then
                placeLblTuple(g.RangeLabel(i), "RangeLabel", rngTxt, g, i, rowPanel)
            Else
                Dim tmp As (Label, String) = (Nothing, Nothing)
                placeLblTuple(tmp, "RangeLabel", rngTxt, g, i, rowPanel)
            End If

            If g.Nominal IsNot Nothing AndAlso i < g.Nominal.Length Then
                placeLblTuple(g.Nominal(i), "Nominal", nomTxt, g, i, rowPanel)
            Else
                Dim tmp As (Label, String) = (Nothing, Nothing)
                placeLblTuple(tmp, "Nominal", nomTxt, g, i, rowPanel)
            End If

            If g.Unit IsNot Nothing AndAlso i < g.Unit.Length Then
                placeLblTuple(g.Unit(i), "Unit", untTxt, g, i, rowPanel)
            Else
                Dim tmp As (Label, String) = (Nothing, Nothing)
                placeLblTuple(tmp, "Unit", untTxt, g, i, rowPanel)
            End If

            If g.Frequency IsNot Nothing AndAlso i < g.Frequency.Length Then
                placeLblTuple(g.Frequency(i), "Frequency", frqTxt, g, i, rowPanel)
            Else
                Dim tmp As (Label, String) = (Nothing, Nothing)
                placeLblTuple(tmp, "Frequency", frqTxt, g, i, rowPanel)
            End If

            If g.FreqUnit IsNot Nothing AndAlso i < g.FreqUnit.Length Then
                placeLblTuple(g.FreqUnit(i), "FreqUnit", fuTxt, g, i, rowPanel)
            Else
                Dim tmp As (Label, String) = (Nothing, Nothing)
                placeLblTuple(tmp, "FreqUnit", fuTxt, g, i, rowPanel)
            End If

            ' --- INPUTS / OUTPUTS (skip if OOB; don't fabricate TBs) ---
            If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length Then placeTbTuple(g.MV1(i), "MV1", g, "MV1", i, rowPanel)
            If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length Then placeTbTuple(g.MV2(i), "MV2", g, "MV2", i, rowPanel)
            If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length Then placeTbTuple(g.MV3(i), "MV3", g, "MV3", i, rowPanel)

            If g.Average IsNot Nothing AndAlso i < g.Average.Length Then placeLbTuple2(g.Average(i), "Average", g, "AVG", i, rowPanel)
            If g.Error IsNot Nothing AndAlso i < g.Error.Length Then placeLbTuple2(g.Error(i), "Error", g, "ERR", i, rowPanel)
            If g.Tolerance IsNot Nothing AndAlso i < g.Tolerance.Length Then placeTbTuple(g.Tolerance(i), "Tolerance", g, "TOL", i, rowPanel)
            If g.UpperLimit IsNot Nothing AndAlso i < g.UpperLimit.Length Then placeTbTuple(g.UpperLimit(i), "UpperLimit", g, "UP", i, rowPanel)
            If g.LowerLimit IsNot Nothing AndAlso i < g.LowerLimit.Length Then placeTbTuple(g.LowerLimit(i), "LowerLimit", g, "LO", i, rowPanel)
            If g.Remarks IsNot Nothing AndAlso i < g.Remarks.Length Then placeTbTuple(g.Remarks(i), "Remarks", g, "REM", i, rowPanel)
            If g.FinalUncDecl IsNot Nothing AndAlso i < g.FinalUncDecl.Length Then placeLbTuple2(g.FinalUncDecl(i), "Final_U", g, "UNC", i, rowPanel)

            fl.Controls.Add(rowPanel)
        End Sub

        ' ---------- Build rows (original insertion order) ----------
        For Each g In ordered
            If g Is Nothing Then Continue For

            Dim headerText = $"Sheet: {g.SheetKey}"
            Dim sheetHdr As New Label() With {
            .AutoSize = False, .Height = 20, .Width = availW,
            .Text = headerText,
            .Font = New Font(Me.Font, FontStyle.Bold)
        }
            fl.Controls.Add(sheetHdr)
            fl.SetFlowBreak(sheetHdr, True)

            Dim nRows As Integer = Math.Max(1, g.TemplateRowCount)
            For i As Integer = 0 To nRows - 1
                addRow(g, i)
            Next
        Next

        fl.AutoScroll = oldScroll
        fl.ResumeLayout()
        fl.PerformLayout()
    End Sub

    Private Sub ApplyCategoriesAndSelection()
        If Groups Is Nothing OrElse Groups.Count = 0 Then Exit Sub

        ' ----- helpers -----
        Dim MakeAddr As Func(Of String, Integer, String) =
        Function(col As String, r As Integer) $"{col}{r}"

        Dim TryGetRow As Func(Of String, Integer) =
        Function(addr As String) As Integer
            If String.IsNullOrWhiteSpace(addr) Then Return -1
            Dim m = System.Text.RegularExpressions.Regex.Match(addr.Trim(), "^\s*[A-Za-z]+(\d+)\s*$")
            If Not m.Success Then Return -1
            Return Integer.Parse(m.Groups(1).Value)
        End Function

        Dim Stamp As Action(Of Control, String) =
        Sub(c As Control, addr As String)
            If c Is Nothing OrElse String.IsNullOrWhiteSpace(addr) Then Exit Sub
            c.Tag = addr : c.AccessibleName = addr
            Dim base As String = If(c.Name, "")
            Dim cut As Integer = base.IndexOf("__", StringComparison.Ordinal)
            If cut >= 0 Then base = base.Substring(0, cut)
            c.Name = base & "__" & addr
        End Sub

        Dim GetAddrLbl As Func(Of (lbl As Label, cell As String)(), Integer, String) =
        Function(arr, i)
            If arr Is Nothing OrElse i < 0 OrElse i >= arr.Length Then Return Nothing
            Return arr(i).cell
        End Function

        Dim GetAddrTb As Func(Of (tb As TextBox, cell As String)(), Integer, String) =
        Function(arr, i)
            If arr Is Nothing OrElse i < 0 OrElse i >= arr.Length Then Return Nothing
            Return arr(i).cell
        End Function

        ' Row from a *specific index* using any mapped cell at that index
        Dim RowFromIndex As Func(Of ParamGroup, Integer, Integer) =
        Function(g As ParamGroup, i As Integer) As Integer
            Dim candidates As New List(Of String) From {
                GetAddrLbl(g.COL_FUNCTION, i),
                GetAddrLbl(g.RangeLabel, i),
                GetAddrLbl(g.Nominal, i),
                GetAddrLbl(g.Unit, i),
                GetAddrLbl(g.Frequency, i),
                GetAddrLbl(g.FreqUnit, i),
                GetAddrTb(g.MV1, i),
                GetAddrTb(g.MV2, i),
                GetAddrTb(g.MV3, i),
                GetAddrTb(g.Tolerance, i),
                GetAddrTb(g.UpperLimit, i),
                GetAddrTb(g.LowerLimit, i),
                GetAddrTb(g.Remarks, i),
                GetAddrLbl(g.Average, i),
                GetAddrLbl(g.Error, i),
                GetAddrLbl(g.FinalUncDecl, i)
            }
            For Each a In candidates
                Dim r = TryGetRow(a)
                If r > 0 Then Return r
            Next
            Return -1
        End Function

        ' Find the first (starting) row for this group:
        ' prefer index 0’s mapped cells; otherwise scan the sheet lightly
        Dim FindStartRow As Func(Of ParamGroup, Object, Integer) =
        Function(g As ParamGroup, ws As Object) As Integer
            Dim r0 = RowFromIndex(g, 0)
            If r0 > 0 Then Return r0

            ' light scan for first non-empty data row (A..F or G..Q)
            Dim cols() As String = {"A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "N", "O", "P", "Q", "AI"}
            For r As Integer = 2 To 200
                For Each col In cols
                    Dim v = CalRowModule.ReadCell(ws, $"{col}{r}")
                    If Not String.IsNullOrWhiteSpace(v) Then Return r
                Next
            Next
            Return -1
        End Function

        For Each kv In Groups
            Dim g = kv.Value
            If g Is Nothing Then Continue For
            Dim sheetName As String = If(String.IsNullOrWhiteSpace(g.SheetKey), kv.Key, g.SheetKey)
            SetSheetsIfChanged(ctxDc, sheetName)
            CalRowModule.WithWorksheet(ctxDc,
            Sub(ws)

                ' how many rows exist in this group
                Dim n As Integer = 0
                n = Math.Max(n, If(g.COL_FUNCTION IsNot Nothing, g.COL_FUNCTION.Length, 0))
                n = Math.Max(n, If(g.RangeLabel IsNot Nothing, g.RangeLabel.Length, 0))
                n = Math.Max(n, If(g.Nominal IsNot Nothing, g.Nominal.Length, 0))
                n = Math.Max(n, If(g.Unit IsNot Nothing, g.Unit.Length, 0))
                n = Math.Max(n, If(g.Frequency IsNot Nothing, g.Frequency.Length, 0))
                n = Math.Max(n, If(g.FreqUnit IsNot Nothing, g.FreqUnit.Length, 0))
                n = Math.Max(n, If(g.MV1 IsNot Nothing, g.MV1.Length, 0))
                n = Math.Max(n, If(g.MV2 IsNot Nothing, g.MV2.Length, 0))
                n = Math.Max(n, If(g.MV3 IsNot Nothing, g.MV3.Length, 0))
                n = Math.Max(n, If(g.Average IsNot Nothing, g.Average.Length, 0))
                n = Math.Max(n, If(g.Error IsNot Nothing, g.Error.Length, 0))
                n = Math.Max(n, If(g.FinalUncDecl IsNot Nothing, g.FinalUncDecl.Length, 0))
                n = Math.Max(n, If(g.Tolerance IsNot Nothing, g.Tolerance.Length, 0))
                n = Math.Max(n, If(g.UpperLimit IsNot Nothing, g.UpperLimit.Length, 0))
                n = Math.Max(n, If(g.LowerLimit IsNot Nothing, g.LowerLimit.Length, 0))
                n = Math.Max(n, If(g.Remarks IsNot Nothing, g.Remarks.Length, 0))
                n = Math.Max(n, Math.Max(1, g.TemplateRowCount))

                ' discover the group start row once
                Dim startRow As Integer = FindStartRow(g, ws)

                For i As Integer = 0 To n - 1
                    Dim excelRow As Integer = RowFromIndex(g, i)
                    If excelRow <= 0 AndAlso startRow > 0 Then excelRow = startRow + i
                    If excelRow <= 0 Then
                        SetRowVisible(g, i, False)
                        Continue For
                    End If

                    ' ----- LEFT (A..F) -----
                    Dim a_fn = CalRowModule.ReadCell(ws, MakeAddr("A", excelRow))
                    Dim b_rng = CalRowModule.ReadCell(ws, MakeAddr("B", excelRow))
                    Dim c_nom = CalRowModule.ReadCell(ws, MakeAddr("C", excelRow))
                    Dim d_unit = CalRowModule.ReadCell(ws, MakeAddr("D", excelRow))
                    Dim e_frq = CalRowModule.ReadCell(ws, MakeAddr("E", excelRow))
                    Dim f_fu = CalRowModule.ReadCell(ws, MakeAddr("F", excelRow))

                    If g.COL_FUNCTION IsNot Nothing AndAlso i < g.COL_FUNCTION.Length Then
                        If String.IsNullOrWhiteSpace(g.COL_FUNCTION(i).cell) Then g.COL_FUNCTION(i).cell = "A" & excelRow
                        If g.COL_FUNCTION(i).lbl IsNot Nothing Then
                            g.COL_FUNCTION(i).lbl.Text = a_fn : g.COL_FUNCTION(i).lbl.Visible = True
                            Stamp(g.COL_FUNCTION(i).lbl, g.COL_FUNCTION(i).cell)
                        End If
                    End If
                    If g.RangeLabel IsNot Nothing AndAlso i < g.RangeLabel.Length Then
                        If String.IsNullOrWhiteSpace(g.RangeLabel(i).cell) Then g.RangeLabel(i).cell = "B" & excelRow
                        If g.RangeLabel(i).lbl IsNot Nothing Then
                            g.RangeLabel(i).lbl.Text = b_rng : g.RangeLabel(i).lbl.Visible = True
                            Stamp(g.RangeLabel(i).lbl, g.RangeLabel(i).cell)
                        End If
                    End If
                    If g.Nominal IsNot Nothing AndAlso i < g.Nominal.Length Then
                        If String.IsNullOrWhiteSpace(g.Nominal(i).cell) Then g.Nominal(i).cell = "C" & excelRow
                        If g.Nominal(i).lbl IsNot Nothing Then
                            g.Nominal(i).lbl.Text = c_nom : g.Nominal(i).lbl.Visible = True
                            Stamp(g.Nominal(i).lbl, g.Nominal(i).cell)
                        End If
                    End If
                    If g.Unit IsNot Nothing AndAlso i < g.Unit.Length Then
                        If String.IsNullOrWhiteSpace(g.Unit(i).cell) Then g.Unit(i).cell = "D" & excelRow
                        If g.Unit(i).lbl IsNot Nothing Then
                            g.Unit(i).lbl.Text = d_unit : g.Unit(i).lbl.Visible = True
                            Stamp(g.Unit(i).lbl, g.Unit(i).cell)
                        End If
                    End If
                    If g.Frequency IsNot Nothing AndAlso i < g.Frequency.Length Then
                        If String.IsNullOrWhiteSpace(g.Frequency(i).cell) Then g.Frequency(i).cell = "E" & excelRow
                        If g.Frequency(i).lbl IsNot Nothing Then
                            g.Frequency(i).lbl.Text = e_frq : g.Frequency(i).lbl.Visible = True
                            Stamp(g.Frequency(i).lbl, g.Frequency(i).cell)
                        End If
                    End If
                    If g.FreqUnit IsNot Nothing AndAlso i < g.FreqUnit.Length Then
                        If String.IsNullOrWhiteSpace(g.FreqUnit(i).cell) Then g.FreqUnit(i).cell = "F" & excelRow
                        If g.FreqUnit(i).lbl IsNot Nothing Then
                            g.FreqUnit(i).lbl.Text = f_fu : g.FreqUnit(i).lbl.Visible = True
                            Stamp(g.FreqUnit(i).lbl, g.FreqUnit(i).cell)
                        End If
                    End If

                    ' ----- RIGHT (computed labels) -----
                    If g.Average IsNot Nothing AndAlso i < g.Average.Length Then
                        If String.IsNullOrWhiteSpace(g.Average(i).cell) Then g.Average(i).cell = "J" & excelRow
                        If g.Average(i).lbl IsNot Nothing Then
                            g.Average(i).lbl.Text = CalRowModule.ReadCell(ws, g.Average(i).cell)
                            g.Average(i).lbl.Visible = True
                            Stamp(g.Average(i).lbl, g.Average(i).cell)
                        End If
                    End If
                    If g.[Error] IsNot Nothing AndAlso i < g.[Error].Length Then
                        If String.IsNullOrWhiteSpace(g.[Error](i).cell) Then g.[Error](i).cell = "K" & excelRow
                        If g.[Error](i).lbl IsNot Nothing Then
                            g.[Error](i).lbl.Text = CalRowModule.ReadCell(ws, g.[Error](i).cell)
                            g.[Error](i).lbl.Visible = True
                            Stamp(g.[Error](i).lbl, g.[Error](i).cell)
                        End If
                    End If
                    If g.FinalUncDecl IsNot Nothing AndAlso i < g.FinalUncDecl.Length Then
                        If String.IsNullOrWhiteSpace(g.FinalUncDecl(i).cell) Then g.FinalUncDecl(i).cell = "AI" & excelRow
                        If g.FinalUncDecl(i).lbl IsNot Nothing Then
                            g.FinalUncDecl(i).lbl.Text = CalRowModule.ReadCell(ws, g.FinalUncDecl(i).cell)
                            g.FinalUncDecl(i).lbl.Visible = True
                            Stamp(g.FinalUncDecl(i).lbl, g.FinalUncDecl(i).cell)
                        End If
                    End If

                    ' ----- RIGHT (inputs/limits/remarks) -----
                    If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length Then
                        If String.IsNullOrWhiteSpace(g.MV1(i).cell) Then g.MV1(i).cell = "G" & excelRow
                        If g.MV1(i).tb IsNot Nothing Then
                            g.MV1(i).tb.Text = CalRowModule.ReadCell(ws, g.MV1(i).cell)
                            g.MV1(i).tb.Visible = True
                            Stamp(g.MV1(i).tb, g.MV1(i).cell)
                        End If
                    End If
                    If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length Then
                        If String.IsNullOrWhiteSpace(g.MV2(i).cell) Then g.MV2(i).cell = "H" & excelRow
                        If g.MV2(i).tb IsNot Nothing Then
                            g.MV2(i).tb.Text = CalRowModule.ReadCell(ws, g.MV2(i).cell)
                            g.MV2(i).tb.Visible = True
                            Stamp(g.MV2(i).tb, g.MV2(i).cell)
                        End If
                    End If
                    If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length Then
                        If String.IsNullOrWhiteSpace(g.MV3(i).cell) Then g.MV3(i).cell = "I" & excelRow
                        If g.MV3(i).tb IsNot Nothing Then
                            g.MV3(i).tb.Text = CalRowModule.ReadCell(ws, g.MV3(i).cell)
                            g.MV3(i).tb.Visible = True
                            Stamp(g.MV3(i).tb, g.MV3(i).cell)
                        End If
                    End If
                    If g.Tolerance IsNot Nothing AndAlso i < g.Tolerance.Length Then
                        If String.IsNullOrWhiteSpace(g.Tolerance(i).cell) Then g.Tolerance(i).cell = "N" & excelRow
                        If g.Tolerance(i).tb IsNot Nothing Then
                            g.Tolerance(i).tb.Text = CalRowModule.ReadCell(ws, g.Tolerance(i).cell)
                            g.Tolerance(i).tb.Visible = True
                            Stamp(g.Tolerance(i).tb, g.Tolerance(i).cell)
                        End If
                    End If
                    If g.UpperLimit IsNot Nothing AndAlso i < g.UpperLimit.Length Then
                        If String.IsNullOrWhiteSpace(g.UpperLimit(i).cell) Then g.UpperLimit(i).cell = "O" & excelRow
                        If g.UpperLimit(i).tb IsNot Nothing Then
                            g.UpperLimit(i).tb.Text = CalRowModule.ReadCell(ws, g.UpperLimit(i).cell)
                            g.UpperLimit(i).tb.Visible = True
                            Stamp(g.UpperLimit(i).tb, g.UpperLimit(i).cell)
                        End If
                    End If
                    If g.LowerLimit IsNot Nothing AndAlso i < g.LowerLimit.Length Then
                        If String.IsNullOrWhiteSpace(g.LowerLimit(i).cell) Then g.LowerLimit(i).cell = "P" & excelRow
                        If g.LowerLimit(i).tb IsNot Nothing Then
                            g.LowerLimit(i).tb.Text = CalRowModule.ReadCell(ws, g.LowerLimit(i).cell)
                            g.LowerLimit(i).tb.Visible = True
                            Stamp(g.LowerLimit(i).tb, g.LowerLimit(i).cell)
                        End If
                    End If
                    If g.Remarks IsNot Nothing AndAlso i < g.Remarks.Length Then
                        If String.IsNullOrWhiteSpace(g.Remarks(i).cell) Then g.Remarks(i).cell = "Q" & excelRow
                        If g.Remarks(i).tb IsNot Nothing Then
                            g.Remarks(i).tb.Text = CalRowModule.ReadCell(ws, g.Remarks(i).cell)
                            g.Remarks(i).tb.Visible = True
                            Stamp(g.Remarks(i).tb, g.Remarks(i).cell)
                        End If
                    End If

                    SetRowVisible(g, i, True)
                Next
            End Sub)
        Next
    End Sub

    ' --- drop-in: set both sheet names only if changed ---
    Private Sub SetSheetsIfChanged(ctx As CalRowModule.RowContext, name As String)
        If ctx Is Nothing OrElse String.IsNullOrWhiteSpace(name) Then Exit Sub

        Dim sameInputs = String.Equals(ctx.SheetInputsName, name, StringComparison.OrdinalIgnoreCase)
        Dim sameFormula = String.Equals(ctx.SheetFormulaName, name, StringComparison.OrdinalIgnoreCase)
        If sameInputs AndAlso sameFormula Then Exit Sub

        ' if your context exposes a suppression flag, briefly silence events
        Dim suppressProp = ctx.GetType().GetProperty("SuppressEvents")
        Dim hadProp As Boolean = False
        If suppressProp IsNot Nothing Then
            Try
                suppressProp.SetValue(ctx, True)
                hadProp = True
            Catch
            End Try
        End If

        ctx.SheetInputsName = name
        ctx.SheetFormulaName = name

        If hadProp Then
            Try : suppressProp.SetValue(ctx, False) : Catch : End Try
        End If
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        ' 1) Save any pending data
        Try
            If ctxDc IsNot Nothing Then
                CalRowModule.SaveToExcel(ctxDc)
            End If
        Catch
            ' Swallow/Log as needed
        End Try

        ' 2) Stop video source safely
        Try
            If videoSource IsNot Nothing Then
                RemoveHandler videoSource.NewFrame, AddressOf Video_NewFrame
                If videoSource.IsRunning Then
                    videoSource.SignalToStop()
                    videoSource.WaitForStop()
                End If
            End If
        Catch
            ' Swallow/Log as needed
        End Try

        ' 3) Close serial port
        Try
            If SerialPort1 IsNot Nothing AndAlso SerialPort1.IsOpen Then
                SerialPort1.Close()
            End If
        Catch
            ' Swallow/Log as needed
        End Try

        MyBase.OnFormClosing(e)
    End Sub

    ' Prefer an EXTERNAL USB webcam if present; otherwise fall back gracefully
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

    ' Read worksheet names from an .xlsx without Excel Interop (no extra refs)
    Private Function GetWorksheetNamesFromXlsx(path As String) As List(Of String)
        Dim names As New List(Of String)
        Try
            If String.IsNullOrWhiteSpace(path) OrElse Not IO.File.Exists(path) Then Return names
            Using pkg = Package.Open(path, FileMode.Open, FileAccess.Read)
                Dim partUri = New Uri("/xl/workbook.xml", UriKind.Relative)
                Dim part = pkg.GetPart(partUri)
                Using s = part.GetStream(FileMode.Open, FileAccess.Read)
                    Dim doc = System.Xml.Linq.XDocument.Load(s)
                    Dim ns = System.Xml.Linq.XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main")
                    Dim sheets = doc.Root.Element(ns + "sheets")
                    If sheets Is Nothing Then Return names
                    For Each sh In sheets.Elements(ns + "sheet")
                        Dim n = CStr(sh.Attribute("name"))
                        If Not String.IsNullOrWhiteSpace(n) Then names.Add(n)
                    Next
                End Using
            End Using
        Catch
            ' ignore and return anything we parsed
        End Try
        Return names
    End Function

#End Region

#Region "Portable Job_Export helpers"

    ' Returns a portable Job_Export folder.
    ' 1) Prefer next to the EXE: <app>\Job_Export
    ' 2) Fallback to: Documents\ASCal\Job_Export (always writable)

    Private Function GetJobExportDir() As String
        Dim dir1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Job_Export")
        Try
            Directory.CreateDirectory(dir1)
            Return dir1
        Catch ex As UnauthorizedAccessException
            Dim dir2 = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ASCal", "Job_Export")
            Directory.CreateDirectory(dir2)
            Return dir2
        End Try
    End Function

    ' !!!!!!!!!!!!>>> CHANGE FILE NAME FORMAT HERE <<<!!!!!!!!!!!!!!!!!!!
    Private Function BuildReportFileName() As String
        ' Examples:
        ' Return $"CalReport_{NormalizeFile(WorkOrderNumber)}.xlsx"
        ' Return $"Cal_{NormalizeFile(WorkOrderNumber)}_{NormalizeFile(SerialNumber)}_{DateTime.Now:yyyyMMdd}.xlsx"
        Return $"CalReport_{NormalizeFile(WorkOrderNumber)}_{NormalizeFile(SerialNumber)}.xlsx"
    End Function

    ' Mirrors a saved file into the Job_Export folder (portable)
    Private Sub CopyToJobExport(sourcePath As String)
        Try
            Dim exportDir = GetJobExportDir()
            Dim dest = Path.Combine(exportDir, Path.GetFileName(sourcePath))
            File.Copy(sourcePath, dest, True)
        Catch ex As Exception
            MessageBox.Show("Saved, but unable to copy to Job_Export: " & ex.Message,
                            "Copy Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    '' Read descriptor labels (Function / Range / Nominal / Unit / Frequency / FreqUnit)
    '' using the same row as the MV cells (we derive the row from MV1/MV2/MV3 mapping).
    'Private Sub ReadDescriptorRow(ws As Object, g As Object, i As Integer)
    '    Dim pg = DirectCast(g, Object)

    '    ' Figure out which row to read (use MV1/MV2/MV3 mapped cells)
    '    Dim rowNum As Integer = -1
    '    Try
    '        If pg.MV1 IsNot Nothing AndAlso i < pg.MV1.Length AndAlso pg.MV1(i).cell IsNot Nothing Then
    '            rowNum = GetRowFromAddr(pg.MV1(i).cell)
    '        ElseIf pg.MV2 IsNot Nothing AndAlso i < pg.MV2.Length AndAlso pg.MV2(i).cell IsNot Nothing Then
    '            rowNum = GetRowFromAddr(pg.MV2(i).cell)
    '        ElseIf pg.MV3 IsNot Nothing AndAlso i < pg.MV3.Length AndAlso pg.MV3(i).cell IsNot Nothing Then
    '            rowNum = GetRowFromAddr(pg.MV3(i).cell)
    '        End If
    '    Catch
    '    End Try
    '    If rowNum <= 0 Then Exit Sub

    'End Sub

#End Region

#Region "Header write (cells mapping)" 'inputs na galing sa calibrate.vb

    Private Sub WriteAllHeaderInputsToExcel_Cells(ws As Object)
        ' Left block
        WriteIfNotEmpty(ws, "L7", WorkOrderNumber)      ' Work Order Number
        WriteIfNotEmpty(ws, "AN7", TechnicianInitials)  ' Technical ID
        WriteIfNotEmpty(ws, "K9", Description)          ' Description
        WriteIfNotEmpty(ws, "K11", Manufacturer)        ' Manufacturer
        WriteIfNotEmpty(ws, "K13", Model)               ' Model
        WriteIfNotEmpty(ws, "K15", SerialNumber)        ' Serial Number
        WriteIfNotEmpty(ws, "K17", Range)               ' Range
        WriteIfNotEmpty(ws, "K19", Readability)         ' Res/Readability
        WriteIfNotEmpty(ws, "K21", PrevSesCalCert)      ' Prev. SES Cal Cert

        ' Right block
        WriteIfNotEmpty(ws, "AL9", ReceivedDate)        ' received date
        WriteIfNotEmpty(ws, "AL11", CalibrationDate)    ' calibration date
        WriteIfNotEmpty(ws, "AL13", OptionsInstalled)   ' options installed
        WriteIfNotEmpty(ws, "AL15", CustomerPO)         ' customers PO
        WriteIfNotEmpty(ws, "AL17", AssetNumber)        ' asset number
        WriteIfNotEmpty(ws, "AL19", AccuracyHeader)     ' accuracy header
        WriteIfNotEmpty(ws, "AL21", PreviousTechnician) ' previous technician

        ' Company
        WriteIfNotEmpty(ws, "H25", CompanyName)         ' company name
        WriteIfNotEmpty(ws, "H27", CompanyAddress)      ' company address

        ' In-house / On-site flags & address
        Dim ct = If(CalibrationType, "").Trim().ToUpperInvariant()
        If ct.Contains("IN-HOUSE") OrElse ct.Contains("INHOUSE") Then
            WriteIfNotEmpty(ws, "AE25", "x")            ' kung in-house ang checked
        ElseIf ct.Contains("ON-SITE") OrElse ct.Contains("ONSITE") Then
            WriteIfNotEmpty(ws, "AE27", "x")            ' kung on-site ang checked
            WriteIfNotEmpty(ws, "AG29", SpecificSite)   ' address ng on site calibration
        End If

        ' Reference Standards (rows 33–34)
        WriteIfNotEmpty(ws, "B33", RefDesc1)            ' reference description 1
        WriteIfNotEmpty(ws, "Q33", RefSN1)              ' reference serial number 1
        WriteIfNotEmpty(ws, "AB33", RefCalRef1)         ' reference cal reference 1
        WriteIfNotEmpty(ws, "AO33", RefDue1)            ' reference due date 1

        WriteIfNotEmpty(ws, "B34", RefDesc2)            ' reference description 2
        WriteIfNotEmpty(ws, "Q34", RefSN2)              ' reference serial number 2
        WriteIfNotEmpty(ws, "AB34", RefCalRef2)         ' reference cal reference 2
        WriteIfNotEmpty(ws, "AO34", RefDue2)            ' reference due date 2

        ' Accessories (rows 37–38)
        WriteIfNotEmpty(ws, "B37", AccDesc1)            ' accesory description 1
        WriteIfNotEmpty(ws, "Q37", AccSN1)              ' accesory serial num 1
        WriteIfNotEmpty(ws, "AB37", AccCalBrand1)       ' accesory brand 1
        WriteIfNotEmpty(ws, "AO37", AccModel1)          ' accesory model 1

        WriteIfNotEmpty(ws, "B38", AccDesc2)            ' accesory description 2
        WriteIfNotEmpty(ws, "Q38", AccSN2)              ' accesory serial num 2
        WriteIfNotEmpty(ws, "AB38", AccCalBrand2)       ' accesory brand 2
        WriteIfNotEmpty(ws, "AO38", AccModel2)          ' accesory model 2

        ' Environmental condition
        WriteIfNotEmpty(ws, "K41", TempStart)       ' Temperature Start
        WriteIfNotEmpty(ws, "K42", TempEnd)         ' Temperature End
        WriteIfNotEmpty(ws, "T41", HumidityStart)   ' Relative Humidity Start
        WriteIfNotEmpty(ws, "T42", HumidityEnd)     ' Relative Humidity End

        WriteIfNotEmpty(ws, "AB40", calMathod)     ' Calibration Method
        WriteIfNotEmpty(ws, "B140", TechnicianName)     ' Technician Name

    End Sub

    Private Sub WriteIfNotEmpty(ws As Object, addr As String, value As String)
        If String.IsNullOrWhiteSpace(value) Then Exit Sub
        WriteCell(ws, addr, value)
    End Sub

#End Region

#Region "Live compute plumbing" 'mag-aactivate eto kapag nagmanual input sa lahat ng fields

    ' === MV textbox change event ===
    ' Recalc when MV changes
    Private Sub OnMvChanged(sender As Object, e As EventArgs)
        Dim tb = TryCast(sender, TextBox)
        Dim g As ParamGroup = Nothing
        Dim i As Integer = -1
        If Not ResolveOwner(tb, g, i) Then Exit Sub

        currentGroup = g
        currentRowIdx = i
        Dim excelRow As Integer = GetRowFromAddr(g.MV3(i).Item2) ' cell addr is Item2
        currentExcelRow = excelRow
        ctxDc.TargetRow = excelRow

        ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, g, i)
        ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, g, i)
        CalRowModule.RecalculateNow(ctxDc)
    End Sub

    ' === HookLiveCompute (Sub) ===
    ' Attach TextChanged to MV boxes
    ' Attach lightweight handlers: MV1/MV2 do NOT compute; MV3 computes when row is complete
    Private Sub HookLiveCompute()
        Dim attach = Sub(arr As (TextBox, String)(), isMv3 As Boolean)
                         If arr Is Nothing Then Exit Sub
                         For Each p In arr
                             If p.Item1 Is Nothing Then Continue For

                             ' Clean out any previous handlers (including the old OnMvChanged)
                             RemoveHandler p.Item1.TextChanged, AddressOf OnMvChanged
                             RemoveHandler p.Item1.TextChanged, AddressOf OnMv12Changed
                             RemoveHandler p.Item1.TextChanged, AddressOf OnMv3Changed

                             If isMv3 Then
                                 AddHandler p.Item1.TextChanged, AddressOf OnMv3Changed
                             Else
                                 AddHandler p.Item1.TextChanged, AddressOf OnMv12Changed
                             End If
                         Next
                     End Sub

        For Each g In Groups.Values
            If g Is Nothing Then Continue For
            attach(g.MV1, False)   ' MV1: no compute
            attach(g.MV2, False)   ' MV2: no compute
            attach(g.MV3, True)    ' MV3: compute when row complete
        Next
    End Sub

    ' MV1/MV2 changes: only remember the context; no Excel compute here
    Private Sub OnMv12Changed(sender As Object, e As EventArgs)
        Dim tb = TryCast(sender, TextBox)
        If tb Is Nothing Then Exit Sub
        Dim g As ParamGroup = Nothing
        Dim i As Integer = -1
        If Not ResolveOwner(tb, g, i) Then Exit Sub
        currentGroup = g
        currentRowIdx = i
        ' Optional UX: auto-advance when field becomes non-empty
        'If tb Is currentGroup.MV1(i).tb AndAlso Not String.IsNullOrWhiteSpace(tb.Text) Then
        '    FocusAdvance(currentGroup, i, tb)
        'End If
    End Sub

    ' MV3 changes: when the row has all 3 MVs filled → compute this row immediately
    Private Sub OnMv3Changed(sender As Object, e As EventArgs)
        Dim tb = TryCast(sender, TextBox)
        If tb Is Nothing Then Exit Sub

        Dim g As ParamGroup = Nothing
        Dim i As Integer = -1
        If Not ResolveOwner(tb, g, i) Then Exit Sub
        If g Is Nothing OrElse i < 0 Then Exit Sub

        currentGroup = g
        currentRowIdx = i

        ' Only compute when the row is complete (MV1, MV2, MV3 all have values)
        If Not IsRowComplete(g, i) Then Exit Sub

        ' Target the row of MV3 and compute now
        currentExcelRow = GetRowFromAddr(g.MV3(i).cell)
        ctxDc.TargetRow = currentExcelRow

        ' Single-row compute runs instantly (per your updated StartRowCompute)
        StartRowCompute(g, i)

        ' Optional UX: move focus to next visible row's MV1
        If g.MV1 IsNot Nothing AndAlso i + 1 < g.MV1.Length Then
            Dim nextTb = g.MV1(i + 1).tb
            If nextTb IsNot Nothing AndAlso nextTb.Visible Then
                nextTb.Focus()
                nextTb.SelectAll()
            End If
        End If
    End Sub

    ' Find owner group/row for an MV textbox
    Private Function ResolveOwner(tb As TextBox, ByRef g As ParamGroup, ByRef row As Integer) As Boolean
        g = Nothing : row = -1
        If tb Is Nothing Then Return False
        For Each gg In Groups.Values
            If gg Is Nothing Then Continue For
            For Each arr In New(TextBox, String)()() {gg.MV1, gg.MV2, gg.MV3}
                If arr Is Nothing Then Continue For
                For i As Integer = 0 To arr.Length - 1
                    If arr(i).Item1 Is tb Then g = gg : row = i : Return True
                Next
            Next
        Next
        Return False
    End Function

    ' === TEMP row timing ===
    Private rowStopwatch As System.Diagnostics.Stopwatch = Nothing

    ' Collect per-row elapsed times in order
    Private rowTimes As New List(Of (Key As String, Elapsed As TimeSpan))

#End Region

#Region "Row helpers & visibility" '---------need ko pang iedit kasi meron mga nagaappear na hindi na select sa calibrate

    Private Sub SetRowVisible(g As ParamGroup, idx As Integer, visible As Boolean)
        If g Is Nothing Then Exit Sub

        Dim showLbl = Sub(a As (lbl As Label, cell As String)())
                          If a Is Nothing OrElse idx >= a.Length Then Exit Sub
                          Dim lb = a(idx).lbl
                          If lb IsNot Nothing Then lb.Visible = visible
                      End Sub
        Dim showTb = Sub(a As (tb As TextBox, cell As String)())
                         If a Is Nothing OrElse idx >= a.Length Then Exit Sub
                         Dim tb = a(idx).tb
                         If tb IsNot Nothing Then
                             tb.Visible = visible
                             tb.TabStop = visible
                         End If
                     End Sub
        Dim showOutLbl = Sub(a As (lbl As Label, cell As String)())
                             If a Is Nothing OrElse idx >= a.Length Then Exit Sub
                             Dim lb = a(idx).lbl : If lb IsNot Nothing Then lb.Visible = visible
                         End Sub

        showLbl(g.RangeLabel) : showLbl(g.COL_FUNCTION) : showLbl(g.Nominal) : showLbl(g.Unit) : showLbl(g.Frequency) : showLbl(g.FreqUnit) : showTb(g.MV1) : showTb(g.MV2) : showTb(g.MV3) : showOutLbl(g.Average) : showOutLbl(g.Error) : showOutLbl(g.FinalUncDecl) : showTb(g.Tolerance) : showTb(g.UpperLimit) : showTb(g.LowerLimit) : showTb(g.Remarks)
    End Sub

    Private Sub FocusAdvance(g As ParamGroup, row As Integer, currentTb As WinForms.TextBox)
        Dim target As WinForms.TextBox = Nothing

        If currentTb Is g.MV1(row).tb Then
            target = g.MV2(row).tb
        ElseIf currentTb Is g.MV2(row).tb Then
            target = g.MV3(row).tb
        ElseIf currentTb Is g.MV3(row).tb Then
            ' Move to next row's MV1
            If row + 1 < g.MV1.Length Then
                target = g.MV1(row + 1).tb
            End If
        End If

        If target IsNot Nothing Then
            target.Focus()
            target.SelectAll()
            ScrollIntoViewDeep(target)

            ' --- Auto-scroll to ensure visibility ---
            Dim scrollParent As WinForms.ScrollableControl = TryCast(target.Parent, WinForms.ScrollableControl)
            If scrollParent IsNot Nothing Then
                scrollParent.ScrollControlIntoView(target)
            End If
        End If
    End Sub

    Private Sub ScrollIntoViewDeep(c As WinForms.Control)
        Dim p As WinForms.Control = c
        While p IsNot Nothing
            Dim sc = TryCast(p, WinForms.ScrollableControl)
            If sc IsNot Nothing AndAlso sc.AutoScroll Then
                sc.ScrollControlIntoView(c)
            End If
            p = p.Parent
        End While
    End Sub

    'treat missing controls as blank
    Private Function IsRowComplete(g As ParamGroup, i As Integer) As Boolean
        If g Is Nothing OrElse i < 0 Then Return False
        If g.MV1 Is Nothing OrElse g.MV2 Is Nothing OrElse g.MV3 Is Nothing Then Return False
        If i >= g.MV1.Length OrElse i >= g.MV2.Length OrElse i >= g.MV3.Length Then Return False

        Dim t1 As String = If(g.MV1(i).tb IsNot Nothing, g.MV1(i).tb.Text, "")
        Dim t2 As String = If(g.MV2(i).tb IsNot Nothing, g.MV2(i).tb.Text, "")
        Dim t3 As String = If(g.MV3(i).tb IsNot Nothing, g.MV3(i).tb.Text, "")

        Return Not String.IsNullOrWhiteSpace(t1) AndAlso
           Not String.IsNullOrWhiteSpace(t2) AndAlso
           Not String.IsNullOrWhiteSpace(t3)
    End Function

    Private Function AreAllVisibleRowsComplete() As Boolean
        For Each h In Groups.Values
            If h Is Nothing OrElse h.MV1 Is Nothing Then Continue For
            For i = 0 To h.MV1.Length - 1
                Dim tb1 = h.MV1(i).tb
                Dim tb2 = If(h.MV2 IsNot Nothing AndAlso i < h.MV2.Length, h.MV2(i).tb, Nothing)
                Dim tb3 = If(h.MV3 IsNot Nothing AndAlso i < h.MV3.Length, h.MV3(i).tb, Nothing)
                If tb1 IsNot Nothing AndAlso tb1.Visible Then
                    If tb2 Is Nothing OrElse tb3 Is Nothing Then Return False
                    If String.IsNullOrWhiteSpace(tb1.Text) OrElse
                       String.IsNullOrWhiteSpace(tb2.Text) OrElse
                       String.IsNullOrWhiteSpace(tb3.Text) Then
                        Return False
                    End If
                End If
            Next
        Next
        Return True
    End Function

#End Region

#Region "Excel interop helpers"

    Private Sub WriteInputsRow(ws As Object, g As ParamGroup, i As Integer)
        If ws Is Nothing OrElse g Is Nothing OrElse i < 0 Then Exit Sub
        If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length AndAlso g.MV1(i).tb IsNot Nothing Then
            WriteCell(ws, g.MV1(i).cell, g.MV1(i).tb.Text)
        End If
        If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length AndAlso g.MV2(i).tb IsNot Nothing Then
            WriteCell(ws, g.MV2(i).cell, g.MV2(i).tb.Text)
        End If
        If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length AndAlso g.MV3(i).tb IsNot Nothing Then
            WriteCell(ws, g.MV3(i).cell, g.MV3(i).tb.Text)
        End If
    End Sub

    'null-safe writes; blank/Formula-not-yet-computed returns ""
    Private Sub ReadOutputsRow(ws As Object, g As ParamGroup, i As Integer)
        If ws Is Nothing OrElse g Is Nothing OrElse i < 0 Then Exit Sub

        ' Labels
        If g.Average IsNot Nothing AndAlso i < g.Average.Length Then
            If g.Average(i).lbl IsNot Nothing Then g.Average(i).lbl.Text = ReadCell(ws, g.Average(i).cell)
        End If

        If g.Error IsNot Nothing AndAlso i < g.Error.Length Then
            If g.Error(i).lbl IsNot Nothing Then g.Error(i).lbl.Text = ReadCell(ws, g.Error(i).cell)
        End If

        If g.FinalUncDecl IsNot Nothing AndAlso i < g.FinalUncDecl.Length Then
            If g.FinalUncDecl(i).lbl IsNot Nothing Then g.FinalUncDecl(i).lbl.Text = ReadCell(ws, g.FinalUncDecl(i).cell)
        End If

        ' TextBoxes
        If g.Tolerance IsNot Nothing AndAlso i < g.Tolerance.Length Then
            If g.Tolerance(i).tb IsNot Nothing Then g.Tolerance(i).tb.Text = ReadCell(ws, g.Tolerance(i).cell)
        End If

        If g.UpperLimit IsNot Nothing AndAlso i < g.UpperLimit.Length Then
            If g.UpperLimit(i).tb IsNot Nothing Then g.UpperLimit(i).tb.Text = ReadCell(ws, g.UpperLimit(i).cell)
        End If

        If g.LowerLimit IsNot Nothing AndAlso i < g.LowerLimit.Length Then
            If g.LowerLimit(i).tb IsNot Nothing Then g.LowerLimit(i).tb.Text = ReadCell(ws, g.LowerLimit(i).cell)
        End If

        If g.Remarks IsNot Nothing AndAlso i < g.Remarks.Length Then
            If g.Remarks(i).tb IsNot Nothing Then
                g.Remarks(i).tb.Text = ReadCell(ws, g.Remarks(i).cell)
                ApplyPassFailColor(g.Remarks(i).tb)
            End If
        End If
    End Sub

    Private Function SafeReadCell(ws As Object, addr As String) As String
        If ws Is Nothing OrElse String.IsNullOrWhiteSpace(addr) Then Return ""
        Try
            Dim cell = CallByName(ws, "Range", CallType.Get, addr)
            ' Excel can return Nothing or non-string; coerce safely
            Dim txtObj = CallByName(cell, "Text", CallType.Get)
            Dim txt = TryCast(txtObj, String)
            If txt Is Nothing Then
                ' Some formulas show empty string before inputs; treat as blank
                Return ""
            End If
            Return txt
        Catch
            ' Bad address / not available yet, etc.
            Return ""
        End Try
    End Function

    'Private Sub WriteAllVisibleInputs(ws As Object, g As ParamGroup)
    '    If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
    '    For i As Integer = 0 To g.MV1.Length - 1
    '        Dim tb1 As TextBox = g.MV1(i).tb
    '        If tb1 IsNot Nothing AndAlso tb1.Visible Then WriteInputsRow(ws, g, i)
    '    Next
    'End Sub

    'Private Sub ReadAllOutputsForVisibleRows(ws As Object, g As ParamGroup)
    '    If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
    '    For i As Integer = 0 To g.MV1.Length - 1
    '        Dim tb1 As TextBox = g.MV1(i).tb
    '        If tb1 IsNot Nothing AndAlso tb1.Visible Then ReadOutputsRow(ws, g, i)
    '    Next
    'End Sub

    Private Sub WriteCell(ws As Object, addr As String, value As String)
        Dim cell = CallByName(ws, "Range", CallType.Get, addr)
        CallByName(cell, "Value", CallType.Let, value)
    End Sub

    'same function name, now null-safe + coercion
    Private Function ReadCell(ws As Object, addr As String) As String
        If ws Is Nothing OrElse String.IsNullOrWhiteSpace(addr) Then Return ""
        Try
            Dim cell = CallByName(ws, "Range", CallType.Get, addr)
            If cell Is Nothing Then Return ""
            Dim txtObj = CallByName(cell, "Text", CallType.Get)
            Dim txt = TryCast(txtObj, String)
            If txt Is Nothing Then Return ""
            Return txt
        Catch
            Return ""
        End Try
    End Function

    Private Function GetRowFromAddr(addr As String) As Integer
        If String.IsNullOrWhiteSpace(addr) Then Return -1
        Dim m = System.Text.RegularExpressions.Regex.Match(addr, "\$?[A-Za-z]+\$?(\d+)")
        If Not m.Success Then Return -1
        Return Integer.Parse(m.Groups(1).Value, Globalization.CultureInfo.InvariantCulture)
    End Function

    Private Sub ApplyPassFailColor(tb As WinForms.TextBox)
        If tb Is Nothing Then Exit Sub
        Dim val = If(tb.Text, "").Trim().ToUpperInvariant()
        Select Case val
            Case "PASS"
                tb.BackColor = Drawing.Color.FromArgb(198, 239, 206) ' green
                tb.ForeColor = Drawing.Color.Black
            Case "FAIL"
                tb.BackColor = Drawing.Color.FromArgb(255, 199, 206) ' red
                tb.ForeColor = Drawing.Color.Black
            Case Else
                tb.BackColor = Drawing.SystemColors.ControlLight
                tb.ForeColor = Drawing.SystemColors.WindowText
        End Select
    End Sub

    Private Function NormalizeFile(s As String) As String
        If String.IsNullOrWhiteSpace(s) Then Return "NA"
        For Each ch In Path.GetInvalidFileNameChars()
            s = s.Replace(ch, "_"c)
        Next
        Return s.Trim()
    End Function

#End Region

#Region "Visibility by category + parameter-row filtering"

    Private Sub SetGroupVisible(g As ParamGroup, visible As Boolean)
        If g Is Nothing Then Exit Sub

        Dim setTb = Sub(arr As (tb As TextBox, cell As String)())
                        If arr Is Nothing Then Exit Sub
                        For Each p In arr
                            If p.tb IsNot Nothing Then
                                p.tb.Visible = visible
                                p.tb.TabStop = visible
                                p.tb.ReadOnly = Not visible
                            End If
                        Next
                    End Sub

        Dim setLbl = Sub(arr As (lbl As Label, cell As String)())
                         If arr Is Nothing Then Exit Sub
                         For Each p In arr
                             If p.lbl IsNot Nothing Then p.lbl.Visible = visible
                         Next
                     End Sub

        Dim setPlain = Sub(arr As (lbl As Label, cell As String)())
                           If arr Is Nothing Then Exit Sub
                           For Each t In arr
                               If t.lbl Is Nothing Then Continue For
                               t.lbl.Font = Me.Font
                               t.lbl.ForeColor = SystemColors.ControlText
                           Next
                       End Sub

        setPlain(g.RangeLabel) : setPlain(g.COL_FUNCTION) : setPlain(g.Nominal)
        setPlain(g.Unit) : setPlain(g.Frequency) : setPlain(g.FreqUnit)

        setTb(g.MV1) : setTb(g.MV2) : setTb(g.MV3)
        setLbl(g.Average) : setLbl(g.Error) : setLbl(g.FinalUncDecl)
        setTb(g.Tolerance) : setTb(g.UpperLimit) : setTb(g.LowerLimit) : setTb(g.Remarks)
    End Sub

    Private Function TrimMatch(rx As System.Text.RegularExpressions.Regex, s As String) As String
        Dim m = rx.Match(s)
        If m.Success Then Return m.Groups(1).Value.Trim()
        Return ""
    End Function

    Private Sub ClearArr(ByRef arr As WinForms.Label())
        If arr Is Nothing Then Exit Sub
        For i = 0 To arr.Length - 1
            If arr(i) IsNot Nothing Then arr(i).Text = ""
        Next
    End Sub

#End Region

#Region "Sir Mel"

    ' =========================================================
    '  Camera + Snipping Tool OCR (NO brand/model logic here)
    '  Focus: Normalize OCR text, detect negative signs,
    '         infer READING (main) vs RANGE (scale) from flat text.
    ' =========================================================

    ' ---------- FIELDS ----------
    Dim tentimes As Integer = 0

    ' --- Camera state (same as calibrate) ---
    Private videoSource As AForge.Video.DirectShow.VideoCaptureDevice

    Private latestFrame As Bitmap
    Private latestFrameLock As New Object()

    Dim bmp As Bitmap

    ' --- TEST BURST (3 shots total) ---

    Private testBurstCopiesRemaining As Integer = 0
    Private burstGroup As ParamGroup = Nothing
    Private burstRow As Integer = -1

    ' --- Click-interval state (TEST MODE) ---
    Private lastCaptureAt As DateTime = DateTime.MinValue

    Private lastClickGroup As Object = Nothing   ' ParamGroup
    Private lastClickRow As Integer = -1
    Private lastNextSlot As Integer = 0          ' 1=MV1, 2=MV2, 3=MV3

    ' For thread-safe UI updates in serial receive (kept for compatibility)
    Delegate Sub SetTextCallback(ByVal [text] As String)

    ' ---------- Win32 Imports ----------
    <DllImport("user32.dll")>
    Private Shared Function ShowWindow(hWnd As IntPtr, nCmdShow As Integer) As Boolean
    End Function

    '<DllImport("user32.dll")>
    'Private Shared Function BlockInput(fBlockIt As Boolean) As Boolean
    'End Function

    Private Const SW_HIDE As Integer = 0
    Private Const SW_SHOW As Integer = 5

    ' ---------- Small helpers ----------
    Private Sub HideSnippingTool()
        Dim snippingProcesses As String() = {"SnippingTool", "SnipAndSketch"}
        For Each procName As String In snippingProcesses
            For Each p As Process In Process.GetProcessesByName(procName)
                Dim h As IntPtr = p.MainWindowHandle
                If h <> IntPtr.Zero Then ShowWindow(h, SW_HIDE)
            Next
        Next
    End Sub

    Private Sub RemoveFocus()
        Dim dummy = Me.Controls("lblDummy")
        If dummy IsNot Nothing Then dummy.Focus()
    End Sub

    ' ---------- Camera preview ----------
    Private Sub Video_NewFrame(sender As Object, eventArgs As AForge.Video.NewFrameEventArgs)
        Dim frame As Bitmap = Nothing
        Try
            frame = DirectCast(eventArgs.Frame.Clone(), Bitmap)

            ' Keep a copy (optional – handy if you also OCR in this form)
            SyncLock latestFrameLock
                If latestFrame IsNot Nothing Then latestFrame.Dispose()
                latestFrame = DirectCast(frame.Clone(), Bitmap)
            End SyncLock

            ' Show in a PictureBox (rename if your control has a different name)
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
        Catch
            ' swallow/optionally log
        Finally
            If frame IsNot Nothing Then frame.Dispose()
        End Try
    End Sub

    Friend WithEvents txtReading As System.Windows.Forms.TextBox

    Private firstClick As Boolean = True
    Private isCapturing As Boolean = False

    ' ---------- Capture Button (merged flow) ----------
    Private Sub BtnCapture_Click(sender As Object, e As EventArgs) Handles BtnCapture.Click
        ' ---- Re-entrancy guard ----
        If isCapturing Then Return
        isCapturing = True

        Try
            ' ---------- Ensure we have a target row ----------
            If currentGroup Is Nothing OrElse currentRowIdx < 0 Then
                For Each pg As ParamGroup In Groups.Values
                    If pg Is Nothing OrElse pg.MV1 Is Nothing Then Continue For
                    For i As Integer = 0 To pg.MV1.Length - 1
                        Dim tb = pg.MV1(i).tb
                        If tb IsNot Nothing AndAlso tb.Visible Then
                            currentGroup = pg
                            currentRowIdx = i
                            Exit For
                        End If
                    Next
                    If currentGroup IsNot Nothing Then Exit For
                Next
                If currentGroup Is Nothing Then
                    MessageBox.Show("No visible parameter rows. Check mappings and filters.", "Capture", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Exit Sub
                End If
            End If

            Dim g As ParamGroup = currentGroup
            Dim r As Integer = currentRowIdx
            Dim capReadingNoUnit As String = ""
            Dim capRangeNoUnit As String = ""

            ' ---------- ONE capture+OCR ----------
            Dim CaptureReadingOnce As Func(Of Boolean) =
        Function() As Boolean
            capReadingNoUnit = "" : capRangeNoUnit = ""

            ' --- Capture frame ---
            Try
                If videoSource IsNot Nothing Then
                    RemoveHandler videoSource.NewFrame, AddressOf Video_NewFrame
                    If videoSource.IsRunning Then
                        videoSource.SignalToStop()
                        videoSource.WaitForStop()
                    End If
                End If
                Dim cam = CreatePreferredCamera()
                If cam IsNot Nothing Then
                    videoSource = cam
                    AddHandler videoSource.NewFrame, AddressOf Video_NewFrame
                    videoSource.Start()
                    Threading.Thread.Sleep(500)
                End If
            Catch
            End Try

            ' --- Save a frame ---
            Dim baseDir As String = "C:\CapImg"
            If Not IO.Directory.Exists(baseDir) Then IO.Directory.CreateDirectory(baseDir)
            Dim capturePath As String = IO.Path.Combine(baseDir, $"{DateTime.Now:yyHHmmss_fff}.jpg")

            Dim toSave As Bitmap = Nothing
            SyncLock latestFrameLock
                If latestFrame IsNot Nothing Then
                    toSave = DirectCast(latestFrame.Clone(), Bitmap)
                End If
            End SyncLock

            If toSave Is Nothing AndAlso PictureBox1.Image IsNot Nothing Then
                toSave = DirectCast(PictureBox1.Image.Clone(), Bitmap)
            End If

            If toSave Is Nothing Then
                MessageBox.Show("Camera frame is empty.", "Capture", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return False
            End If

            Try
                toSave.Save(capturePath, Imaging.ImageFormat.Jpeg)
            Catch ex As Exception
                MessageBox.Show("Failed to save captured image: " & ex.Message, "Capture", MessageBoxButtons.OK, MessageBoxIcon.Error)
                toSave.Dispose()
                Return False
            Finally
                toSave.Dispose()
            End Try

            ' --- Stop camera while OCRing ---
            Try
                If videoSource IsNot Nothing AndAlso videoSource.IsRunning Then
                    videoSource.SignalToStop()
                    videoSource.WaitForStop()
                End If
            Catch
            End Try

            ' --- OCR extraction ---
            DMMtxtparameter.Clear()
            DMMreading.Clear()
            RichTextBox1.Clear()
            RemoveFocus()

            Try
                Dim launched As Boolean = False
                Try
                    Process.Start("C:\Users\dbneri\AppData\Local\Microsoft\WindowsApps\SnippingTool.exe") : launched = True
                Catch
                    Try : Process.Start("SnippingTool.exe") : launched = True : Catch : End Try
                End Try
                If Not launched Then
                    MessageBox.Show("Cannot launch Snipping Tool.", "Snipping Tool", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    Return False
                End If

                Threading.Thread.Sleep(500)
                HideSnippingTool()

                ' --- SendKeys automation ---
                My.Computer.Keyboard.SendKeys("{TAB}", True) : Threading.Thread.Sleep(100)
                My.Computer.Keyboard.SendKeys("{ENTER}", True) : Threading.Thread.Sleep(100)
                My.Computer.Keyboard.SendKeys("{ENTER}", True) : Threading.Thread.Sleep(1000)
                My.Computer.Keyboard.SendKeys(capturePath, True) : Threading.Thread.Sleep(100)
                My.Computer.Keyboard.SendKeys("{ENTER}", True) : Threading.Thread.Sleep(1000)
                My.Computer.Keyboard.SendKeys("{TAB}{TAB}{TAB}{RIGHT}{ENTER}", True)
                Threading.Thread.Sleep(1500)
                My.Computer.Keyboard.SendKeys("{TAB}{TAB}{TAB}{ENTER}", True)
                Threading.Thread.Sleep(100)

                RichTextBox1.Paste()
                Dim raw As String = NormalizeOcrText(RichTextBox1.Text)

                If String.IsNullOrWhiteSpace(DMMtxtparameter.Text) Then
                    If raw.IndexOf("V", StringComparison.OrdinalIgnoreCase) >= 0 Then
                        DMMtxtparameter.Text = "V"
                    ElseIf raw.IndexOf("A", StringComparison.OrdinalIgnoreCase) >= 0 Then
                        DMMtxtparameter.Text = "A"
                    ElseIf raw.IndexOf("Ω", StringComparison.OrdinalIgnoreCase) >= 0 OrElse raw.IndexOf("OHM", StringComparison.OrdinalIgnoreCase) >= 0 Then
                        DMMtxtparameter.Text = "Ω"
                    End If
                End If

                Dim tokens = ExtractOcrTokens(raw)
                Dim expectedUnit As String = If(String.IsNullOrWhiteSpace(DMMtxtparameter.Text), "", DMMtxtparameter.Text.Trim().ToUpperInvariant())
                Dim readingStr As String = "", rangeStr As String = ""
                PickReadingAndRange(tokens, expectedUnit, readingStr, rangeStr)

                If readingStr = "" Then Return False

                capReadingNoUnit = StripUnitSuffix(readingStr)
                DMMreading.Text = capReadingNoUnit

                If rangeStr <> "" Then
                    capRangeNoUnit = StripUnitSuffix(rangeStr)
                End If
            Finally
                For Each procName In New String() {"SnippingTool", "SnipAndSketch"}
                    For Each p As Process In Process.GetProcessesByName(procName)
                        Try : p.Kill() : p.WaitForExit() : Catch : End Try
                    Next
                Next
            End Try

            Return True
        End Function

            ' ---------- Find next empty (scans current group → next groups with wrap-around) ----------
            Dim orderedGroups = Groups.OrderBy(Function(kv) kv.Value.SheetBase) _
                                  .ThenBy(Function(kv) kv.Value.SheetIndex) _
                                  .ToList()

            Dim FindNextEmpty As Func(Of ParamGroup, Integer, (found As Boolean, row As Integer, slot As Integer, tb As TextBox)) =
        Function(pg As ParamGroup, startRow As Integer)
            ' search inside one group
            Dim SearchInGroup As Func(Of ParamGroup, Integer, (Boolean, Integer, Integer, TextBox)) =
            Function(pgx As ParamGroup, r0 As Integer)
                Dim maxRows As Integer = Math.Max(Math.Max(If(pgx.MV1?.Length, 0), If(pgx.MV2?.Length, 0)), If(pgx.MV3?.Length, 0))
                For rr As Integer = Math.Max(0, r0) To maxRows - 1
                    If pgx.MV1 IsNot Nothing AndAlso rr < pgx.MV1.Length Then
                        Dim t = pgx.MV1(rr).tb
                        If t IsNot Nothing AndAlso t.Visible AndAlso String.IsNullOrWhiteSpace(t.Text) Then
                            Return (True, rr, 1, t)
                        End If
                    End If
                    If pgx.MV2 IsNot Nothing AndAlso rr < pgx.MV2.Length Then
                        Dim t = pgx.MV2(rr).tb
                        If t IsNot Nothing AndAlso t.Visible AndAlso String.IsNullOrWhiteSpace(t.Text) Then
                            Return (True, rr, 2, t)
                        End If
                    End If
                    If pgx.MV3 IsNot Nothing AndAlso rr < pgx.MV3.Length Then
                        Dim t = pgx.MV3(rr).tb
                        If t IsNot Nothing AndAlso t.Visible AndAlso String.IsNullOrWhiteSpace(t.Text) Then
                            Return (True, rr, 3, t)
                        End If
                    End If
                Next
                Return (False, -1, 0, Nothing)
            End Function

            ' 1) current group from startRow
            Dim hit = SearchInGroup(pg, startRow)
            If hit.Item1 Then Return hit

            ' 2) other groups after current
            Dim curIdx As Integer = orderedGroups.FindIndex(Function(kv) kv.Value Is pg)
            If curIdx < 0 Then curIdx = 0

            For gi As Integer = curIdx + 1 To orderedGroups.Count - 1
                Dim nextPg = orderedGroups(gi).Value
                hit = SearchInGroup(nextPg, 0)
                If hit.Item1 Then
                    currentGroup = nextPg ' hop group
                    Return hit
                End If
            Next

            ' 3) wrap-around to the beginning
            For gi As Integer = 0 To Math.Max(0, curIdx - 1)
                Dim nextPg = orderedGroups(gi).Value
                hit = SearchInGroup(nextPg, 0)
                If hit.Item1 Then
                    currentGroup = nextPg
                    Return hit
                End If
            Next

            Return (False, -1, 0, Nothing)
        End Function

            ' ---------- Fill ALL available MV textboxes across groups ----------
            Dim curRow As Integer = r
            Do
                Dim nxt = FindNextEmpty(g, curRow)
                If Not nxt.found Then Exit Do

                ' follow group hop if it occurred
                If Not (currentGroup Is g) Then g = currentGroup
                r = nxt.row

                ' capture → write for this single slot
                Dim tries As Integer = 0
                Const MAX_TRIES As Integer = 3
                Dim ok As Boolean = False
                Do
                    ok = CaptureReadingOnce()
                    If ok Then Exit Do
                    tries += 1
                    Application.DoEvents()
                    Threading.Thread.Sleep(120)
                Loop While tries < MAX_TRIES

                If Not ok Then
                    ' skip to look after this row to avoid looping the same slot
                    curRow = r + 1
                    Continue Do
                End If

                nxt.tb.Text = capReadingNoUnit
                If Not String.IsNullOrWhiteSpace(capRangeNoUnit) Then
                    DMMrange.Text = capRangeNoUnit
                    Me.Range = capRangeNoUnit
                End If

                ' if this row is now complete, compute it
                If IsRowComplete(g, r) Then
                    currentGroup = g
                    currentRowIdx = r

                    ' pick any available MV cell to resolve Excel row safely
                    Dim addr As String = Nothing
                    If g.MV3 IsNot Nothing AndAlso r < g.MV3.Length Then addr = g.MV3(r).cell
                    If String.IsNullOrWhiteSpace(addr) AndAlso g.MV2 IsNot Nothing AndAlso r < g.MV2.Length Then addr = g.MV2(r).cell
                    If String.IsNullOrWhiteSpace(addr) AndAlso g.MV1 IsNot Nothing AndAlso r < g.MV1.Length Then addr = g.MV1(r).cell

                    If Not String.IsNullOrWhiteSpace(addr) Then
                        currentExcelRow = GetRowFromAddr(addr)
                        ctxDc.TargetRow = currentExcelRow
                        StartRowCompute(g, r)   ' make sure StartRowCompute re-arms Pre/After each time
                    End If
                End If

                ' next search starts at this row again (in case more MV slots are empty on it)
                curRow = r
            Loop

            ' ---------- Automatically advance focus (optional) ----------
            Try
                FocusAdvance(g, r, Nothing)
            Catch ex As Exception
                Debug.WriteLine("FocusAdvance failed: " & ex.Message)
            Finally
                Dim ng As ParamGroup = Nothing
                Dim nr As Integer = -1
                If TryResolveFocus(ng, nr) Then
                    currentGroup = ng
                    currentRowIdx = nr
                End If
            End Try

            ' ---------- Resume live camera preview ----------
            Try
                If videoSource IsNot Nothing Then
                    RemoveHandler videoSource.NewFrame, AddressOf Video_NewFrame
                    If videoSource.IsRunning Then
                        videoSource.SignalToStop()
                        videoSource.WaitForStop()
                    End If
                End If

                Dim cam2 = CreatePreferredCamera()
                If cam2 IsNot Nothing Then
                    videoSource = cam2
                    AddHandler videoSource.NewFrame, AddressOf Video_NewFrame
                    videoSource.Start()
                End If
            Catch
            End Try
        Finally
            isCapturing = False ' ---- release the guard ----
        End Try
    End Sub

    ' Resolve the ParamGroup/row from a focused TextBox control.
    Private Function TryResolveFocus(ByRef outGroup As ParamGroup, ByRef outRow As Integer, Optional ctrl As Control = Nothing) As Boolean
        outGroup = Nothing : outRow = -1
        If ctrl Is Nothing Then ctrl = Me.ActiveControl
        Dim tb As TextBox = TryCast(ctrl, TextBox)
        If tb Is Nothing Then Return False

        For Each pg As ParamGroup In Groups.Values
            If pg Is Nothing Then Continue For
            Dim n As Integer = Math.Max(Math.Max(If(pg.MV1?.Length, 0), If(pg.MV2?.Length, 0)), If(pg.MV3?.Length, 0))
            For i As Integer = 0 To n - 1
                If (pg.MV1 IsNot Nothing AndAlso i < pg.MV1.Length AndAlso pg.MV1(i).tb Is tb) OrElse
               (pg.MV2 IsNot Nothing AndAlso i < pg.MV2.Length AndAlso pg.MV2(i).tb Is tb) OrElse
               (pg.MV3 IsNot Nothing AndAlso i < pg.MV3.Length AndAlso pg.MV3(i).tb Is tb) Then
                    outGroup = pg : outRow = i
                    Return True
                End If
            Next
        Next
        Return False
    End Function

    ' ---------- OCR Text Normalization ----------
    Private Function NormalizeOcrText(s As String) As String
        If s Is Nothing Then Return ""
        Dim t As String = s
        ' Unicode minus & dashes -> ASCII hyphen
        t = t.Replace(ChrW(&H2212), "-").Replace("–", "-").Replace("—", "-")
        ' Remove spaces after sign ("- 12.3" -> "-12.3")
        t = System.Text.RegularExpressions.Regex.Replace(t, "([+\-])\s+(?=\d)", "$1")
        ' Normalize decimals to "."
        t = t.Replace(",", ".")
        Return t
    End Function

    ' ---------- Tokenize numbers (with optional SI + Unit) ----------
    Private Class OcrToken
        Public LineIndex As Integer
        Public Raw As String
        Public Sign As Integer      ' -1, 0, +1
        Public Value As Double      ' scaled to base unit if SI prefix present
        Public Unit As String       ' "V","A","Ω","OHM","HZ",""
        Public HasDecimal As Boolean
        Public LineText As String

        Public NumText As String
    End Class

    Private Function ExtractOcrTokens(text As String) As List(Of OcrToken)
        Dim list As New List(Of OcrToken)

        If String.IsNullOrWhiteSpace(text) Then Return list

        Dim lines = text.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
        Dim rx = New System.Text.RegularExpressions.Regex(
            "([+\-]?)\s*(\d+(?:\.\d+)?)\s*(m|µ|u|k|M)?\s*(V|A|Ω|OHM|HZ)?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase)

        For i As Integer = 0 To lines.Length - 1
            Dim ln = lines(i)
            For Each m As System.Text.RegularExpressions.Match In rx.Matches(ln)
                If Not m.Success Then Continue For
                Dim signTxt = m.Groups(1).Value
                Dim numTxt = m.Groups(2).Value
                Dim siTxt = m.Groups(3).Value
                Dim unitTxt = m.Groups(4).Value

                If String.IsNullOrWhiteSpace(numTxt) Then Continue For

                Dim val As Double
                If Not Double.TryParse(numTxt, Globalization.NumberStyles.Any, Globalization.CultureInfo.InvariantCulture, val) Then Continue For

                Dim mult As Double = 1.0
                Select Case siTxt
                    Case "m" : mult = 0.001
                    Case "µ", "u" : mult = 0.000001
                    Case "k" : mult = 1000.0
                    Case "M" : mult = 1000000.0
                End Select
                val *= mult

                Dim tok As New OcrToken With {
                    .LineIndex = i,
                    .Raw = m.Value.Trim(),
                    .Sign = If(signTxt = "-", -1, If(signTxt = "+", 1, 0)),
                    .Value = val,
                    .Unit = unitTxt.ToUpperInvariant(),
                    .HasDecimal = numTxt.Contains("."),
                    .LineText = ln,
                    .NumText = numTxt
                }
                list.Add(tok)
            Next
        Next
        Return list
    End Function

    ' ---------- Range heuristics ----------
    Private Function StandardRangesFor(unitKey As String) As Double()
        Select Case unitKey
            Case "V" : Return New Double() {2, 6, 20, 60, 200, 600, 1000}
            Case "A" : Return New Double() {0.002, 0.02, 0.2, 2, 10}
            Case "Ω", "OHM" : Return New Double() {200, 2000, 20000, 200000, 2000000, 20000000}
            Case Else : Return Array.Empty(Of Double)()
        End Select
    End Function

    Private Function IsRangeLike(val As Double, unitKey As String) As Boolean
        Dim ranges = StandardRangesFor(unitKey)
        If ranges Is Nothing OrElse ranges.Length = 0 Then Return False
        For Each r In ranges
            If r = 0 Then Continue For
            If Math.Abs(val - r) <= Math.Max(0.02 * r, If(r < 10, 0.5, 1.0)) Then Return True
        Next
        Return False
    End Function

    ' Score how likely a token is the main READING
    Private Function ScoreReading(tok As OcrToken, expectedUnit As String) As Double
        Dim s As Double = 0
        s += Math.Min(8, tok.Raw.Length) * 0.6      ' longer with decimals → likely main
        If tok.HasDecimal Then s += 0.8
        If Not IsRangeLike(tok.Value, If(tok.Unit = "", expectedUnit, tok.Unit)) Then s += 1.2
        If expectedUnit <> "" AndAlso (tok.Unit = "" OrElse tok.Unit.StartsWith(expectedUnit, StringComparison.OrdinalIgnoreCase)) Then s += 0.8
        Dim l = tok.LineText.ToUpperInvariant()
        If l.Contains("AUTO") OrElse l.Contains("LOZ") OrElse l.Contains("RANGE") Then s -= 0.7
        Return s
    End Function

    ' Score how likely a token is a RANGE label
    Private Function ScoreRange(tok As OcrToken, expectedUnit As String) As Double
        Dim s As Double = 0
        Dim unitKey = If(tok.Unit = "", expectedUnit, tok.Unit)
        If IsRangeLike(tok.Value, unitKey) Then s += 2.0
        Dim l = tok.LineText.ToUpperInvariant()
        If l.Contains("AUTO") OrElse l.Contains("RANGE") OrElse l.Contains("AUTO VOLT") Then s += 0.8
        s += Math.Max(0, 6 - Math.Log10(Math.Max(0.000001, tok.Value + 1))) * 0.2 ' smaller values look “range-like”
        Return s
    End Function

    ' Pick best reading and best range from tokens
    Private Sub PickReadingAndRange(tokens As List(Of OcrToken),
                                    expectedUnit As String,
                                    ByRef readingOut As String,
                                    ByRef rangeOut As String)
        readingOut = "" : rangeOut = ""
        If tokens Is Nothing OrElse tokens.Count = 0 Then Exit Sub

        Dim rBest As (tok As OcrToken, score As Double) = (Nothing, Double.NegativeInfinity)
        Dim rngBest As (tok As OcrToken, score As Double) = (Nothing, Double.NegativeInfinity)

        For Each t In tokens
            Dim rs = ScoreReading(t, expectedUnit)
            If rs > rBest.score Then rBest = (t, rs)
            Dim gs = ScoreRange(t, expectedUnit)
            If gs > rngBest.score Then rngBest = (t, gs)
        Next

        If rBest.tok IsNot Nothing Then
            Dim sign = If(rBest.tok.Sign < 0, "-", If(rBest.tok.Sign > 0, "+", ""))
            Dim unit = If(String.IsNullOrEmpty(rBest.tok.Unit), expectedUnit, rBest.tok.Unit)
            ' was: rBest.tok.Value.ToString("G", ...)
            Dim num = If(String.IsNullOrEmpty(rBest.tok.NumText),
                 rBest.tok.Value.ToString("G", Globalization.CultureInfo.InvariantCulture),
                 rBest.tok.NumText)
            readingOut = (sign & num & If(unit = "", "", " " & unit)).Trim()
        End If

        If rngBest.tok IsNot Nothing Then
            Dim unit = If(String.IsNullOrEmpty(rngBest.tok.Unit), expectedUnit, rngBest.tok.Unit)
            Dim ranges = StandardRangesFor(unit)
            If ranges.Length > 0 Then
                Dim nearest = ranges.OrderBy(Function(v) Math.Abs(v - rngBest.tok.Value)).First()
                rangeOut = nearest.ToString("G", Globalization.CultureInfo.InvariantCulture) &
                           If(unit = "", "", " " & unit)
            Else
                rangeOut = rngBest.tok.Value.ToString("G", Globalization.CultureInfo.InvariantCulture) &
                           If(unit = "", "", " " & unit)
            End If
        End If
    End Sub

    Private Function StripUnitSuffix(s As String) As String
        If String.IsNullOrWhiteSpace(s) Then Return s
        ' remove a single trailing unit token like V, A, Ω, OHM, HZ (case-insensitive)
        Return System.Text.RegularExpressions.Regex.Replace(
        s.Trim(),
        "\s*(V|A|Ω|OHM|HZ)$",
        "",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase
    )
    End Function

    ' Global variable to track focused textbox
    Private currentFocusedTextBox As TextBox

    ' Event handler to track focus change
    Private Sub TextBox_Enter(sender As Object, e As EventArgs)
        currentFocusedTextBox = CType(sender, TextBox)
    End Sub

#End Region

    Private Sub ButtonDisable_Click(sender As Object, e As EventArgs) Handles ButtonDisable.Click

        ' === cancel pending Excel hooks so nothing fires after abort ===
        If ctxDc IsNot Nothing Then
            Try
                ctxDc.PreCalculate = Nothing
            Catch
            End Try
            Try
                ctxDc.AfterCalculate = Nothing
            Catch
            End Try
            Try
                ctxDc.TargetRow = -1
            Catch
            End Try
        End If

        ' === UI back to idle ===
        Me.Cursor = Cursors.Default

        Try
            Dim btnCap As Button = TryCast(Me.Controls.Find("Capture", True).FirstOrDefault(), Button)
            If btnCap IsNot Nothing Then btnCap.Enabled = True
        Catch
        End Try

        Try
            If ButtonDisable IsNot Nothing Then ButtonDisable.Enabled = False
        Catch
        End Try
    End Sub

End Class