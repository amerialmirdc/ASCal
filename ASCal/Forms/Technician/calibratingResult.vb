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

    ' was: Private dcComputeTimer As System.Windows.Forms.Timer
    Private dcComputeTimer As WinForms.Timer

    Private nomSeqTimer As WinForms.Timer = Nothing
    Private testBurstTimer As WinForms.Timer = Nothing

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

    ' --- minimal fields used by OnDcComputeTimerTick ---
    Private nomSeqActive As Boolean = False

    Private nomSeqWaitingCompute As Boolean = False

    Private runActive As Boolean = False
    Private runTotalRows As Integer = 0
    Private runComputedRows As Integer = 0
    Private computedKeys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

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

    ' --- fast single-row compute path (moved out of TEMP) ---
    Private dcTargetRowForTick As Integer = -1

    ' Accepts 1..N row indices. Single-row → compute now; multi-row → timer.
    Private Sub StartRowCompute(g As ParamGroup, rowIndices As IEnumerable(Of Integer))
        If g Is Nothing OrElse ctxDc Is Nothing Then Exit Sub
        Dim rows = rowIndices?.Where(Function(r) r >= 0).Distinct().OrderBy(Function(r) r).ToList()
        If rows Is Nothing OrElse rows.Count = 0 Then Exit Sub

        ' Resolve target Excel row from the last requested index.
        Dim lastIdx As Integer = rows.Last()
        Dim targetAddr As String = Nothing
        If g.MV3 IsNot Nothing AndAlso lastIdx < g.MV3.Length AndAlso g.MV3(lastIdx).cell IsNot Nothing Then
            targetAddr = g.MV3(lastIdx).cell
        ElseIf g.MV2 IsNot Nothing AndAlso lastIdx < g.MV2.Length AndAlso g.MV2(lastIdx).cell IsNot Nothing Then
            targetAddr = g.MV2(lastIdx).cell
        ElseIf g.MV1 IsNot Nothing AndAlso lastIdx < g.MV1.Length AndAlso g.MV1(lastIdx).cell IsNot Nothing Then
            targetAddr = g.MV1(lastIdx).cell
        End If
        If String.IsNullOrWhiteSpace(targetAddr) Then Exit Sub

        dcComputeTimer.Stop()
        Me.Cursor = Cursors.WaitCursor

        dcTargetRowForTick = GetRowFromAddr(targetAddr)
        ctxDc.TargetRow = dcTargetRowForTick

        Dim groupLocal = g
        ctxDc.PreCalculate = Sub(ws)
                                 For Each i In rows
                                     WriteInputsRow(ws, groupLocal, i)
                                 Next
                             End Sub

        ctxDc.AfterCalculate = Sub(ws)
                                   For Each i In rows
                                       ReadOutputsRow(ws, groupLocal, i)
                                   Next
                               End Sub

        If rowStopwatch Is Nothing Then rowStopwatch = New System.Diagnostics.Stopwatch()
        rowStopwatch.Reset()
        rowStopwatch.Start()

        If rows.Count = 1 Then
            ' Instant compute for single-row (e.g., after MV3 is filled).
            Try
                CalRowModule.RecalculateNow(ctxDc)
            Finally
                Me.Cursor = Cursors.Default
            End Try
        Else
            ' Batch compute keeps your async timer flow.
            dcComputeTimer.Start()
        End If
    End Sub

    Private Sub StartRowCompute(g As ParamGroup, rowIdx As Integer)
        StartRowCompute(g, New Integer() {rowIdx})
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
    .hostControls = Me.Controls
}
        CalRowModule.Initialize(ctxDc)

        ' ====== SHEET DISCOVERY & MAPPING (include ALL sheets) ======
        Groups.Clear()

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

        ' ================= LIVE COMPUTE WIRING =================
        dcComputeTimer = New WinForms.Timer() With {.Interval = 500}
        AddHandler dcComputeTimer.Tick, AddressOf OnDcComputeTimerTick

        ' ================= PRIME FIRST ROW =================
        Dim firstGroup = Groups.Values.FirstOrDefault(Function(g) g IsNot Nothing AndAlso g.MV3 IsNot Nothing AndAlso g.MV3.Length > 0)
        If firstGroup IsNot Nothing Then
            currentGroup = firstGroup
            currentRowIdx = 0
            currentExcelRow = GetRowFromAddr(firstGroup.MV3(0).cell)
            ctxDc.TargetRow = currentExcelRow
            ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, firstGroup, currentRowIdx)
            ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, firstGroup, currentRowIdx)
            CalRowModule.RecalculateNow(ctxDc)
        End If

        HookLiveCompute()
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

    ' Treat a row as “empty” only if there are no descriptor texts
    ' AND no MV textboxes exist for that index (or all are Nothing).
    Private Function IsRowTrulyEmpty(g As ParamGroup, i As Integer) As Boolean
        If g Is Nothing Then Return True

        Dim descEmpty As Boolean =
        (g.COL_FUNCTION Is Nothing OrElse i >= g.COL_FUNCTION.Length OrElse g.COL_FUNCTION(i).lbl Is Nothing OrElse String.IsNullOrWhiteSpace(g.COL_FUNCTION(i).lbl.Text)) AndAlso
        (g.RangeLabel Is Nothing OrElse i >= g.RangeLabel.Length OrElse g.RangeLabel(i).lbl Is Nothing OrElse String.IsNullOrWhiteSpace(g.RangeLabel(i).lbl.Text)) AndAlso
        (g.Nominal Is Nothing OrElse i >= g.Nominal.Length OrElse g.Nominal(i).lbl Is Nothing OrElse String.IsNullOrWhiteSpace(g.Nominal(i).lbl.Text)) AndAlso
        (g.Unit Is Nothing OrElse i >= g.Unit.Length OrElse g.Unit(i).lbl Is Nothing OrElse String.IsNullOrWhiteSpace(g.Unit(i).lbl.Text)) AndAlso
        (g.Frequency Is Nothing OrElse i >= g.Frequency.Length OrElse g.Frequency(i).lbl Is Nothing OrElse String.IsNullOrWhiteSpace(g.Frequency(i).lbl.Text)) AndAlso
        (g.FreqUnit Is Nothing OrElse i >= g.FreqUnit.Length OrElse g.FreqUnit(i).lbl Is Nothing OrElse String.IsNullOrWhiteSpace(g.FreqUnit(i).lbl.Text))

        Dim mvMissing As Boolean =
        (g.MV1 Is Nothing OrElse i >= g.MV1.Length OrElse g.MV1(i).tb Is Nothing) AndAlso
        (g.MV2 Is Nothing OrElse i >= g.MV2.Length OrElse g.MV2(i).tb Is Nothing) AndAlso
        (g.MV3 Is Nothing OrElse i >= g.MV3.Length OrElse g.MV3(i).tb Is Nothing)

        Return descEmpty AndAlso mvMissing
    End Function

    ' --- Render your existing preview UI into a specific FlowLayoutPanel ---
    ' This is your current PopulatePreviewPanel logic, trimmed to render into "fl".
    Private Sub PopulatePreview(Optional target As FlowLayoutPanel = Nothing)
        ' Resolve target
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

        ' Robust width (tabs can be 0 on first layout)
        Dim availW As Integer = fl.DisplayRectangle.Width
        If availW <= 0 Then
            availW = If(fl.Parent IsNot Nothing, fl.Parent.ClientSize.Width, 800)
        End If
        availW = Math.Max(200, availW - fl.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth)

        ' ===== Header =====
        Dim colLeft As New Dictionary(Of String, Integer)
        Dim colWidth As New Dictionary(Of String, Integer)

        Dim hdr As New Panel() With {.Height = 22, .Width = availW}
        Dim x As Integer = 0
        Dim addHdr = Sub(text As String, left As Integer, width As Integer)
                         Dim lbl As New Label() With {
                         .AutoSize = False, .Text = text, .Left = left, .Top = 2,
                         .Width = width, .Font = New Font(Me.Font, FontStyle.Bold)
                     }
                         hdr.Controls.Add(lbl)
                     End Sub
        Dim addCol = Sub(key As String, w As Integer)
                         addHdr(key, x, w) : colLeft(key) = x : colWidth(key) = w : x += w
                     End Sub

        addCol("Function", 100)
        addCol("RangeLabel", 80)
        addCol("Nominal", 80)
        addCol("Unit", 80)
        addCol("Frequency", 80)
        addCol("FreqUnit", 80)
        addCol("MV1", 80)
        addCol("MV2", 80)
        addCol("MV3", 80)
        addCol("Average", 120)
        addCol("Error", 120)
        addCol("Tolerance", 120)
        addCol("UpperLimit", 120)
        addCol("LowerLimit", 120)
        addCol("Remarks", 100)
        addCol("Final_U", 120)

        fl.Controls.Add(hdr)
        fl.SetFlowBreak(hdr, True)

        ' ===== Row builder (single row, no Ensure* helpers) =====
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

        ' Pull texts already set by ApplyCategoriesAndSelection()
        Dim rngTxt As String = If(g.RangeLabel IsNot Nothing AndAlso i < g.RangeLabel.Length AndAlso g.RangeLabel(i).lbl IsNot Nothing, g.RangeLabel(i).lbl.Text, "")
        Dim fnTxt As String = If(g.COL_FUNCTION IsNot Nothing AndAlso i < g.COL_FUNCTION.Length AndAlso g.COL_FUNCTION(i).lbl IsNot Nothing, g.COL_FUNCTION(i).lbl.Text, "")
        Dim nomTxt As String = If(g.Nominal IsNot Nothing AndAlso i < g.Nominal.Length AndAlso g.Nominal(i).lbl IsNot Nothing, g.Nominal(i).lbl.Text, "")
        Dim untTxt As String = If(g.Unit IsNot Nothing AndAlso i < g.Unit.Length AndAlso g.Unit(i).lbl IsNot Nothing, g.Unit(i).lbl.Text, "")
        Dim frqTxt As String = If(g.Frequency IsNot Nothing AndAlso i < g.Frequency.Length AndAlso g.Frequency(i).lbl IsNot Nothing, g.Frequency(i).lbl.Text, "")
        Dim fuTxt As String = If(g.FreqUnit IsNot Nothing AndAlso i < g.FreqUnit.Length AndAlso g.FreqUnit(i).lbl IsNot Nothing, g.FreqUnit(i).lbl.Text, "")

        ' Tuple-based label placer
        Dim placeLblTuple = Sub(ByRef tup As (Label, String), key As String, value As String, baseName As String)
                                If tup.Item1 Is Nothing Then
                                    tup.Item1 = New Label() With {.Name = $"{baseName}_{i}", .AutoSize = False, .Height = 20}
                                End If
                                tup.Item1.Text = value
                                tup.Item1.Visible = True
                                tup.Item1.Parent = rowPanel
                                tup.Item1.Left = colLeft(key)
                                tup.Item1.Top = 4
                                tup.Item1.Width = colWidth(key)
                            End Sub

        ' ---- Use a DISTINCT scratch tuple per missing column (no reuse!) ----
        Dim tmpFn As (Label, String)
        Dim tmpRng As (Label, String)
        Dim tmpNom As (Label, String)
        Dim tmpUnt As (Label, String)
        Dim tmpFrq As (Label, String)
        Dim tmpFU As (Label, String)

        If g.COL_FUNCTION IsNot Nothing AndAlso i < g.COL_FUNCTION.Length Then
            placeLblTuple(g.COL_FUNCTION(i), "Function", fnTxt, "COL_FUNCTION")
        Else
            placeLblTuple(tmpFn, "Function", fnTxt, "COL_FUNCTION")
        End If

        If g.RangeLabel IsNot Nothing AndAlso i < g.RangeLabel.Length Then
            placeLblTuple(g.RangeLabel(i), "RangeLabel", rngTxt, "RANGE")
        Else
            placeLblTuple(tmpRng, "RangeLabel", rngTxt, "RANGE")
        End If

        If g.Nominal IsNot Nothing AndAlso i < g.Nominal.Length Then
            placeLblTuple(g.Nominal(i), "Nominal", nomTxt, "NOM")
        Else
            placeLblTuple(tmpNom, "Nominal", nomTxt, "NOM")
        End If

        If g.Unit IsNot Nothing AndAlso i < g.Unit.Length Then
            placeLblTuple(g.Unit(i), "Unit", untTxt, "UNIT")
        Else
            placeLblTuple(tmpUnt, "Unit", untTxt, "UNIT")
        End If

        If g.Frequency IsNot Nothing AndAlso i < g.Frequency.Length Then
            placeLblTuple(g.Frequency(i), "Frequency", frqTxt, "FREQ")
        Else
            placeLblTuple(tmpFrq, "Frequency", frqTxt, "FREQ")
        End If

        If g.FreqUnit IsNot Nothing AndAlso i < g.FreqUnit.Length Then
            placeLblTuple(g.FreqUnit(i), "FreqUnit", fuTxt, "FUNIT")
        Else
            placeLblTuple(tmpFU, "FreqUnit", fuTxt, "FUNIT")
        End If

        ' --- ReDim guards for MV/results ---
        If g.MV1 Is Nothing OrElse i >= g.MV1.Length Then ReDim Preserve g.MV1(Math.Max(i, If(g.MV1?.Length, 0)))
        If g.MV2 Is Nothing OrElse i >= g.MV2.Length Then ReDim Preserve g.MV2(Math.Max(i, If(g.MV2?.Length, 0)))
        If g.MV3 Is Nothing OrElse i >= g.MV3.Length Then ReDim Preserve g.MV3(Math.Max(i, If(g.MV3?.Length, 0)))
        If g.Tolerance Is Nothing OrElse i >= g.Tolerance.Length Then ReDim Preserve g.Tolerance(Math.Max(i, If(g.Tolerance?.Length, 0)))
        If g.UpperLimit Is Nothing OrElse i >= g.UpperLimit.Length Then ReDim Preserve g.UpperLimit(Math.Max(i, If(g.UpperLimit?.Length, 0)))
        If g.LowerLimit Is Nothing OrElse i >= g.LowerLimit.Length Then ReDim Preserve g.LowerLimit(Math.Max(i, If(g.LowerLimit?.Length, 0)))
        If g.Remarks Is Nothing OrElse i >= g.Remarks.Length Then ReDim Preserve g.Remarks(Math.Max(i, If(g.Remarks?.Length, 0)))
        If g.Average Is Nothing OrElse i >= g.Average.Length Then ReDim Preserve g.Average(Math.Max(i, If(g.Average?.Length, 0)))
        If g.Error Is Nothing OrElse i >= g.Error.Length Then ReDim Preserve g.Error(Math.Max(i, If(g.Error?.Length, 0)))
        If g.FinalUncDecl Is Nothing OrElse i >= g.FinalUncDecl.Length Then ReDim Preserve g.FinalUncDecl(Math.Max(i, If(g.FinalUncDecl?.Length, 0)))

        ' placers for TB/LB
        Dim placeTbTuple = Sub(ByRef tup As (TextBox, String), key As String, baseName As String)
                               If tup.Item1 Is Nothing Then
                                   tup.Item1 = New TextBox() With {.Name = $"{baseName}_{i}", .Width = colWidth(key) - 6}
                               End If
                               tup.Item1.Visible = True
                               tup.Item1.Parent = rowPanel
                               tup.Item1.Left = colLeft(key) + 3
                               tup.Item1.Top = 2
                           End Sub
        Dim placeLbTuple2 = Sub(ByRef tup As (Label, String), key As String, baseName As String)
                                If tup.Item1 Is Nothing Then
                                    tup.Item1 = New Label() With {.Name = $"{baseName}_{i}", .AutoSize = False, .Height = 20}
                                End If
                                tup.Item1.Visible = True
                                tup.Item1.Parent = rowPanel
                                tup.Item1.Left = colLeft(key)
                                tup.Item1.Top = 4
                                tup.Item1.Width = colWidth(key)
                            End Sub

        ' MV inputs / results
        placeTbTuple(g.MV1(i), "MV1", "MV1")
        placeTbTuple(g.MV2(i), "MV2", "MV2")
        placeTbTuple(g.MV3(i), "MV3", "MV3")
        placeLbTuple2(g.Average(i), "Average", "AVG")
        placeLbTuple2(g.Error(i), "Error", "ERR")
        placeTbTuple(g.Tolerance(i), "Tolerance", "TOL")
        placeTbTuple(g.UpperLimit(i), "UpperLimit", "UP")
        placeTbTuple(g.LowerLimit(i), "LowerLimit", "LO")
        placeTbTuple(g.Remarks(i), "Remarks", "REM")
        placeLbTuple2(g.FinalUncDecl(i), "Final_U", "UNC")

        fl.Controls.Add(rowPanel)
    End Sub

        ' ===== Build rows inline =====
        Dim totalPanels As Integer = 0

        For Each kv In Groups
            Dim sheetName = kv.Key
            Dim g = kv.Value
            If g Is Nothing Then Continue For

            ' --- per-sheet header ---
            Dim sheetHdr As New Label() With {
                .AutoSize = False, .Height = 20, .Width = availW,
                .Text = $"Sheet: {sheetName}",
                .Font = New Font(Me.Font, FontStyle.Bold)
            }
            fl.Controls.Add(sheetHdr)
            fl.SetFlowBreak(sheetHdr, True)

            ' --- how many rows? take max across mapped arrays ---
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

            ' >>> ensure we render at least as many rows as the sheet has
            n = Math.Max(n, Math.Max(1, g.TemplateRowCount))

            ' --- add all rows for this sheet ---
            For i As Integer = 0 To n - 1
                addRow(g, i)
                totalPanels += 1
            Next
        Next

        fl.AutoScroll = oldScroll
        fl.ResumeLayout()
        fl.PerformLayout()

    End Sub

    Private Sub ApplyCategoriesAndSelection()

        ' ---------- tiny helpers ----------
        Dim TrimMatch As Func(Of System.Text.RegularExpressions.Regex, String, String) =
    Function(rx As System.Text.RegularExpressions.Regex, src As String) As String
        If rx Is Nothing OrElse String.IsNullOrEmpty(src) Then Return ""
        Dim m = rx.Match(src)
        If Not m.Success OrElse m.Groups.Count < 2 Then Return ""
        Return m.Groups(1).Value.Trim()
    End Function

        Dim NormTxt As Func(Of String, String) =
    Function(s As String) As String
        s = If(s, "").Trim()
        s = System.Text.RegularExpressions.Regex.Replace(s, "\s+", " ")
        Return s.ToUpperInvariant()
    End Function

        Dim NormNum As Func(Of String, String) =
    Function(s As String) As String
        s = If(s, "")
        s = System.Text.RegularExpressions.Regex.Replace(s, "(?<=\d)\s+(?=\d)", ".") ' 6 45 -> 6.45
        s = s.Replace(" ", "").Replace(",", ".")
        s = System.Text.RegularExpressions.Regex.Replace(s, "([+-]?[0-9]*\.?[0-9]+).*", "$1")
        Return s
    End Function

        Dim MakeAddr As Func(Of String, Integer, String) =
    Function(col As String, r As Integer) $"{col}{r}"

        ' map preview row index -> real Excel row number (from any MV mapping)
        Dim RowFromGroupIndex As Func(Of ParamGroup, Integer, Integer) =
    Function(g As ParamGroup, i As Integer) As Integer
        Dim addr As String = Nothing
        If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length Then addr = g.MV1(i).cell
        If String.IsNullOrWhiteSpace(addr) AndAlso g.MV2 IsNot Nothing AndAlso i < g.MV2.Length Then addr = g.MV2(i).cell
        If String.IsNullOrWhiteSpace(addr) AndAlso g.MV3 IsNot Nothing AndAlso i < g.MV3.Length Then addr = g.MV3(i).cell
        If String.IsNullOrWhiteSpace(addr) Then Return -1
        Return GetRowFromAddr(addr)
    End Function

        ' helper to get array length safely
        Dim L As Func(Of Object, Integer) =
    Function(a As Object) As Integer
        If a Is Nothing Then Return 0
        Dim t = a.GetType()
        If t.IsArray Then Return CType(a, Array).Length
        Return 0
    End Function

        ' ---------- per-sheet fill: switch to each group's sheet, then read & write ----------
        For Each kv In Groups
            Dim sheetName = kv.Key
            Dim g = kv.Value
            If g Is Nothing Then Continue For

            ' point context to this sheet
            ctxDc.SheetInputsName = sheetName
            ctxDc.SheetFormulaName = sheetName

            CalRowModule.WithWorksheet(ctxDc,
        Sub(ws As Object)

            ' set label if present; always buffer into tuple Item2 if slot exists
            Dim SetOrBuffer = Sub(ByRef arr As (Label, String)(), idx As Integer, value As String)
                                  If arr IsNot Nothing AndAlso idx < arr.Length Then
                                      arr(idx).Item2 = value
                                      If arr(idx).Item1 IsNot Nothing Then
                                          arr(idx).Item1.Text = value
                                      End If
                                  End If
                              End Sub

            ' determine number of rows = max length across ALL mapped columns
            Dim n As Integer = 0
            n = Math.Max(n, L(g.COL_FUNCTION))
            n = Math.Max(n, L(g.RangeLabel))
            n = Math.Max(n, L(g.Nominal))
            n = Math.Max(n, L(g.Unit))
            n = Math.Max(n, L(g.Frequency))
            n = Math.Max(n, L(g.FreqUnit))

            n = Math.Max(n, L(g.MV1))
            n = Math.Max(n, L(g.MV2))
            n = Math.Max(n, L(g.MV3))

            ' if you map outputs/limits/remarks, include them too:
            n = Math.Max(n, L(g.Average))
            n = Math.Max(n, L(g.Error))
            n = Math.Max(n, L(g.FinalUncDecl))
            n = Math.Max(n, L(g.Tolerance))
            n = Math.Max(n, L(g.UpperLimit))
            n = Math.Max(n, L(g.LowerLimit))
            n = Math.Max(n, L(g.Remarks))

            If n = 0 Then Return
            n = Math.Max(n, Math.Max(1, g.TemplateRowCount))
            ' --- FILL RIGHT-SIDE FIELDS FROM THEIR MAPPED CELLS (if present) ---
            Dim writeTbFromCell = Sub(ByRef arr As (tb As WinForms.TextBox, cell As String)(), idx As Integer)
                                      If arr Is Nothing OrElse idx >= arr.Length Then Exit Sub
                                      If arr(idx).tb Is Nothing Then Exit Sub                 ' bail before ReadCell
                                      Dim addr = arr(idx).cell
                                      If String.IsNullOrWhiteSpace(addr) Then Exit Sub
                                      Dim val = CalRowModule.ReadCell(ws, addr)
                                      arr(idx).tb.Text = CStr(val)
                                  End Sub

            Dim writeLbFromCell = Sub(ByRef arr As (lbl As WinForms.Label, cell As String)(), idx As Integer)
                                      If arr Is Nothing OrElse idx >= arr.Length Then Exit Sub
                                      If arr(idx).lbl Is Nothing Then Exit Sub                ' bail before ReadCell
                                      Dim addr = arr(idx).cell
                                      If String.IsNullOrWhiteSpace(addr) Then Exit Sub
                                      Dim val = CalRowModule.ReadCell(ws, addr)
                                      arr(idx).lbl.Text = CStr(val)
                                  End Sub

            ' --- per-row pass ---
            For i As Integer = 0 To n - 1
                Dim excelRow = RowFromGroupIndex(g, i)
                If excelRow > 0 Then
                    ' READ TEMPLATE A..F via ReadCell (from this sheet)
                    Dim a_fn As String = CalRowModule.ReadCell(ws, MakeAddr("A", excelRow))
                    Dim b_rng As String = CalRowModule.ReadCell(ws, MakeAddr("B", excelRow))
                    Dim c_nom As String = CalRowModule.ReadCell(ws, MakeAddr("C", excelRow))
                    Dim d_unit As String = CalRowModule.ReadCell(ws, MakeAddr("D", excelRow))
                    Dim e_frq As String = CalRowModule.ReadCell(ws, MakeAddr("E", excelRow))
                    Dim f_frqU As String = CalRowModule.ReadCell(ws, MakeAddr("F", excelRow))

                    ' WRITE LEFT LABELS (label-if-present + buffer Item2)
                    SetLabelTextIfPresent(GetMappedLabel(g.COL_FUNCTION, i), a_fn) : SetOrBuffer(g.COL_FUNCTION, i, a_fn)
                    SetLabelTextIfPresent(GetMappedLabel(g.RangeLabel, i), b_rng) : SetOrBuffer(g.RangeLabel, i, b_rng)
                    SetLabelTextIfPresent(GetMappedLabel(g.Nominal, i), c_nom) : SetOrBuffer(g.Nominal, i, c_nom)
                    SetLabelTextIfPresent(GetMappedLabel(g.Unit, i), d_unit) : SetOrBuffer(g.Unit, i, d_unit)
                    SetLabelTextIfPresent(GetMappedLabel(g.Frequency, i), e_frq) : SetOrBuffer(g.Frequency, i, e_frq)
                    SetLabelTextIfPresent(GetMappedLabel(g.FreqUnit, i), f_frqU) : SetOrBuffer(g.FreqUnit, i, f_frqU)
                End If

                ' right-side fields (only if controls exist)
                writeLbFromCell(g.Average, i)
                writeLbFromCell(g.[Error], i)
                writeLbFromCell(g.FinalUncDecl, i)

                writeTbFromCell(g.Tolerance, i)
                writeTbFromCell(g.UpperLimit, i)
                writeTbFromCell(g.LowerLimit, i)
                writeTbFromCell(g.Remarks, i)

                ' show row
                SetRowVisible(g, i, True)
            Next
        End Sub)
        Next

        ' Refresh the preview
        Dim fl = TryCast(Me.Controls.Find("previewcalibrating", True).FirstOrDefault(), FlowLayoutPanel)
        fl?.PerformLayout()
        fl?.Refresh()

    End Sub

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
        Return $"CalibrationReport_{NormalizeFile(WorkOrderNumber)}_{NormalizeFile(SerialNumber)}.xlsx"
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

    ' Read descriptor labels (Function / Range / Nominal / Unit / Frequency / FreqUnit)
    ' using the same row as the MV cells (we derive the row from MV1/MV2/MV3 mapping).
    Private Sub ReadDescriptorRow(ws As Object, g As Object, i As Integer)
        Dim pg = DirectCast(g, Object)

        ' Figure out which row to read (use MV1/MV2/MV3 mapped cells)
        Dim rowNum As Integer = -1
        Try
            If pg.MV1 IsNot Nothing AndAlso i < pg.MV1.Length AndAlso pg.MV1(i).cell IsNot Nothing Then
                rowNum = GetRowFromAddr(pg.MV1(i).cell)
            ElseIf pg.MV2 IsNot Nothing AndAlso i < pg.MV2.Length AndAlso pg.MV2(i).cell IsNot Nothing Then
                rowNum = GetRowFromAddr(pg.MV2(i).cell)
            ElseIf pg.MV3 IsNot Nothing AndAlso i < pg.MV3.Length AndAlso pg.MV3(i).cell IsNot Nothing Then
                rowNum = GetRowFromAddr(pg.MV3(i).cell)
            End If
        Catch
        End Try
        If rowNum <= 0 Then Exit Sub

    End Sub

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

    Private Sub OnDcComputeTimerTick(sender As Object, e As EventArgs)
        dcComputeTimer.Stop()
        Try
            If dcTargetRowForTick > 0 Then ctxDc.TargetRow = dcTargetRowForTick
            CalRowModule.RecalculateNow(ctxDc)
        Finally
            Me.Cursor = Cursors.Default

            ' Inline “did anything compute?” check using existing fields
            If currentGroup IsNot Nothing AndAlso currentRowIdx >= 0 Then
                Dim anyComputed As Boolean = False

                If currentGroup.Average IsNot Nothing AndAlso currentRowIdx < currentGroup.Average.Length Then
                    If currentGroup.Average(currentRowIdx).lbl IsNot Nothing AndAlso
           Not String.IsNullOrWhiteSpace(currentGroup.Average(currentRowIdx).lbl.Text) Then anyComputed = True
                End If
                If Not anyComputed AndAlso currentGroup.Error IsNot Nothing AndAlso currentRowIdx < currentGroup.Error.Length Then
                    If currentGroup.Error(currentRowIdx).lbl IsNot Nothing AndAlso
           Not String.IsNullOrWhiteSpace(currentGroup.Error(currentRowIdx).lbl.Text) Then anyComputed = True
                End If
                If Not anyComputed AndAlso currentGroup.FinalUncDecl IsNot Nothing AndAlso currentRowIdx < currentGroup.FinalUncDecl.Length Then
                    If currentGroup.FinalUncDecl(currentRowIdx).lbl IsNot Nothing AndAlso
           Not String.IsNullOrWhiteSpace(currentGroup.FinalUncDecl(currentRowIdx).lbl.Text) Then anyComputed = True
                End If

                ' If nothing populated in the Preview controls, reuse your existing preview/list UI refresh if you already have one.
                ' (No new helpers added; just call your existing routine if present.)
                ' Example (keep the name you already use):
                ' If Not anyComputed Then RefreshPreviewUI() Else RefreshPreviewRowUI(currentGroup, currentRowIdx)
            End If
            ' Continue the nominal sequence after the compute for that row finishes
            If nomSeqActive AndAlso nomSeqWaitingCompute Then
                nomSeqWaitingCompute = False
                If nomSeqTimer IsNot Nothing Then
                    nomSeqTimer.Stop()
                    nomSeqTimer.Start()
                End If
            End If

        End Try

    End Sub

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

        showLbl(g.RangeLabel) : showLbl(g.COL_FUNCTION) : showLbl(g.Nominal)
        showLbl(g.Unit) : showLbl(g.Frequency) : showLbl(g.FreqUnit)

        showTb(g.MV1) : showTb(g.MV2) : showTb(g.MV3)
        showOutLbl(g.Average) : showOutLbl(g.Error) : showOutLbl(g.FinalUncDecl)
        showTb(g.Tolerance) : showTb(g.UpperLimit) : showTb(g.LowerLimit) : showTb(g.Remarks)
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

    Private Sub WriteAllVisibleInputs(ws As Object, g As ParamGroup)
        If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
        For i As Integer = 0 To g.MV1.Length - 1
            Dim tb1 As TextBox = g.MV1(i).tb
            If tb1 IsNot Nothing AndAlso tb1.Visible Then WriteInputsRow(ws, g, i)
        Next
    End Sub

    Private Sub ReadAllOutputsForVisibleRows(ws As Object, g As ParamGroup)
        If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
        For i As Integer = 0 To g.MV1.Length - 1
            Dim tb1 As TextBox = g.MV1(i).tb
            If tb1 IsNot Nothing AndAlso tb1.Visible Then ReadOutputsRow(ws, g, i)
        Next
    End Sub

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
                    My.Computer.Keyboard.SendKeys("{ENTER}", True) : Threading.Thread.Sleep(1500)
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

            ' ---------- Fill MV slots ----------
            Dim slots As New List(Of TextBox) From {
            If(g.MV1 IsNot Nothing AndAlso r < g.MV1.Length, g.MV1(r).tb, Nothing),
            If(g.MV2 IsNot Nothing AndAlso r < g.MV2.Length, g.MV2(r).tb, Nothing),
            If(g.MV3 IsNot Nothing AndAlso r < g.MV3.Length, g.MV3(r).tb, Nothing)
        }

            Dim s As Integer = 0
            While s < slots.Count
                Dim targetTb = slots(s)
                If targetTb Is Nothing OrElse Not String.IsNullOrWhiteSpace(targetTb.Text) Then
                    s += 1 : Continue While
                End If

                Dim tries As Integer = 0
                Const MAX_TRIES As Integer = 3
                Do
                    If CaptureReadingOnce() Then Exit Do
                    tries += 1
                    Application.DoEvents()
                    Threading.Thread.Sleep(150)
                Loop While tries < MAX_TRIES

                If tries >= MAX_TRIES Then
                    s += 1 : Continue While
                End If

                targetTb.Text = capReadingNoUnit
                If Not String.IsNullOrWhiteSpace(capRangeNoUnit) Then
                    DMMrange.Text = capRangeNoUnit
                    Me.Range = capRangeNoUnit
                End If

                If IsRowComplete(g, r) Then
                    currentGroup = g
                    currentRowIdx = r
                    currentExcelRow = GetRowFromAddr(g.MV3(r).cell)
                    ctxDc.TargetRow = currentExcelRow
                    StartRowCompute(g, r)
                    Exit While
                End If

                s += 1
            End While

            ' ---------- Automatically advance focus ----------
            Try
                FocusAdvance(g, r, Nothing)
            Catch ex As Exception
                Debug.WriteLine("FocusAdvance failed: " & ex.Message)
            Finally
                ' After advancing focus, update currentGroup/currentRowIdx (and TargetRow) to match the new caret
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

    Private Sub OnReadingTextChanged(sender As Object, e As EventArgs)
        Dim val = DMMreading.Text
        If String.IsNullOrWhiteSpace(val) Then Exit Sub
        txtReading.Text = val
        AutoApplyReadingToCurrentRow(val)
    End Sub

    Private Sub FrmMain_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        If videoSource IsNot Nothing AndAlso videoSource.IsRunning Then
            videoSource.SignalToStop()
            videoSource.WaitForStop()
        End If
        Dim snip() As String = {"SnippingTool", "SnipAndSketch"}
        For Each procName As String In snip
            For Each p As Process In Process.GetProcessesByName(procName)
                Try : p.Kill() : p.WaitForExit() : Catch : End Try
            Next
        Next
        'BlockInput(False)
    End Sub

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

    'Private Function ResolveOrGuessCurrentTarget(ByRef g As ParamGroup, ByRef row As Integer) As Boolean
    '    ' reuse current
    '    g = currentGroup : row = currentRowIdx
    '    If g IsNot Nothing AndAlso row >= 0 Then Return True

    '    ' infer from UI
    '    Dim p As String = If(DMMtxtparameter.Text, "").Trim().ToUpperInvariant()
    '    If p = "OHM" Then p = "Ω"
    '    Dim mode As String = ""
    '    Dim mCtrl = Me.Controls.Find("DMMmode", True).FirstOrDefault()
    '    If TypeOf mCtrl Is TextBox Then mode = DirectCast(mCtrl, TextBox).Text.Trim().ToUpperInvariant()

    '    If AllGroups IsNot Nothing AndAlso AllGroups.Count > 0 Then
    '        'If p <> "" Then g = GetGroupBy(p, mode)
    '        If g Is Nothing Then
    '            ' fallback: first visible row in any selected group
    '            For Each cand In Groups.Values
    '                If cand Is Nothing OrElse cand.MV1 Is Nothing Then Continue For
    '                For i = 0 To cand.MV1.Length - 1
    '                    Dim tb = cand.MV1(i).tb
    '                    If tb IsNot Nothing AndAlso tb.Visible Then g = cand : row = i : Return True
    '                Next
    '            Next
    '        End If
    '    End If

    '    If g Is Nothing Then Return False

    '    row = 0
    '    If g.MV1 IsNot Nothing Then
    '        For i = 0 To g.MV1.Length - 1
    '            Dim tb = g.MV1(i).tb
    '            If tb IsNot Nothing AndAlso tb.Visible Then row = i : Exit For
    '        Next
    '    End If
    '    Return True
    'End Function

    Private Sub OnTestBurstTick(sender As Object, e As EventArgs)
        ' stop if invalid lock
        If burstGroup Is Nothing OrElse burstRow < 0 _
       OrElse burstGroup.MV3 Is Nothing _
       OrElse burstRow >= burstGroup.MV3.Length _
       OrElse burstGroup.MV3(burstRow).tb Is Nothing Then
            testBurstTimer.Stop() : Exit Sub
        End If

        ' stop early if MV3 already filled
        Dim mv3tb = burstGroup.MV3(burstRow).tb
        If Not String.IsNullOrWhiteSpace(mv3tb.Text) Then
            testBurstTimer.Stop() : Exit Sub
        End If

        Dim val = DMMreading.Text
        If String.IsNullOrWhiteSpace(val) Then
            testBurstTimer.Stop() : Exit Sub
        End If

        ' ---- write explicitly to the locked row (MV2 then MV3) ----
        Dim wrote As Boolean = False
        If burstGroup.MV1 IsNot Nothing AndAlso burstRow < burstGroup.MV1.Length Then
            Dim tb1 = burstGroup.MV1(burstRow).tb
            If tb1 IsNot Nothing AndAlso String.IsNullOrWhiteSpace(tb1.Text) Then
                tb1.Text = val : wrote = True
            End If
        End If
        If Not wrote AndAlso burstGroup.MV2 IsNot Nothing AndAlso burstRow < burstGroup.MV2.Length Then
            Dim tb2 = burstGroup.MV2(burstRow).tb
            If tb2 IsNot Nothing AndAlso String.IsNullOrWhiteSpace(tb2.Text) Then
                tb2.Text = val : wrote = True
            End If
        End If
        If Not wrote AndAlso burstGroup.MV3 IsNot Nothing AndAlso burstRow < burstGroup.MV3.Length Then
            Dim tb3 = burstGroup.MV3(burstRow).tb
            If tb3 IsNot Nothing AndAlso String.IsNullOrWhiteSpace(tb3.Text) Then
                tb3.Text = val : wrote = True
            End If
        End If

        ' compute the locked row if now complete
        If IsRowComplete(burstGroup, burstRow) Then
            currentGroup = burstGroup
            currentRowIdx = burstRow
            currentExcelRow = GetRowFromAddr(burstGroup.MV3(burstRow).cell)
            ctxDc.TargetRow = currentExcelRow
            StartRowCompute(burstGroup, burstRow)
        End If

        testBurstCopiesRemaining -= 1
        If testBurstCopiesRemaining <= 0 Then
            testBurstTimer.Stop()
        End If
    End Sub

    Private Sub WriteToSpecificSlot(g As ParamGroup, row As Integer, slot As Integer, val As String)
        ' Declare the variable
        Dim reading As String = val ' Or assign it to whatever value you want
        Dim wrote As Boolean = False

        ' Safety: ensure row and group exist
        If g Is Nothing OrElse row < 0 Then Exit Sub

        ' Write to the first empty MV in the specified row (considering MV1, MV2, MV3)
        Dim rowTarget As Integer = row ' Row index to target

        ' Check MV1 for empty TextBox
        If g.MV1 IsNot Nothing AndAlso rowTarget < g.MV1.Length Then
            Dim tb = g.MV1(rowTarget).tb
            If tb IsNot Nothing AndAlso String.IsNullOrWhiteSpace(tb.Text) Then
                tb.Text = reading
                wrote = True
            End If
        End If

        ' If not written, check MV2 for empty TextBox
        If Not wrote AndAlso g.MV2 IsNot Nothing AndAlso rowTarget < g.MV2.Length Then
            Dim tb = g.MV2(rowTarget).tb
            If tb IsNot Nothing AndAlso String.IsNullOrWhiteSpace(tb.Text) Then
                tb.Text = reading
                wrote = True
            End If
        End If

        ' If not written, check MV3 for empty TextBox
        If Not wrote AndAlso g.MV3 IsNot Nothing AndAlso rowTarget < g.MV3.Length Then
            Dim tb = g.MV3(rowTarget).tb
            If tb IsNot Nothing AndAlso String.IsNullOrWhiteSpace(tb.Text) Then
                tb.Text = reading
                wrote = True
            End If
        End If

        ' After writing, advance focus to the next visible row (if applicable)
        If wrote Then
            ' Move to the next row (next visible TextBox in MV1, MV2, or MV3)
            MoveFocusToNextRow(g, rowTarget)
        End If
    End Sub

    ' Helper to dynamically move focus to the next visible row
    Private Sub MoveFocusToNextRow(g As ParamGroup, currentRow As Integer)
        If g Is Nothing Then Exit Sub

        ' Find the next visible row in MV1, MV2, or MV3
        Dim nextRow As Integer = -1

        ' Try to find the next visible row in MV1
        If g.MV1 IsNot Nothing Then
            For i As Integer = currentRow + 1 To g.MV1.Length - 1
                If g.MV1(i).tb IsNot Nothing AndAlso g.MV1(i).tb.Visible Then
                    nextRow = i
                    Exit For
                End If
            Next
        End If

        ' If no row found in MV1, check MV2
        If nextRow = -1 AndAlso g.MV2 IsNot Nothing Then
            For i As Integer = currentRow + 1 To g.MV2.Length - 1
                If g.MV2(i).tb IsNot Nothing AndAlso g.MV2(i).tb.Visible Then
                    nextRow = i
                    Exit For
                End If
            Next
        End If

        ' If still no row found, check MV3
        If nextRow = -1 AndAlso g.MV3 IsNot Nothing Then
            For i As Integer = currentRow + 1 To g.MV3.Length - 1
                If g.MV3(i).tb IsNot Nothing AndAlso g.MV3(i).tb.Visible Then
                    nextRow = i
                    Exit For
                End If
            Next
        End If

        ' If a valid next row is found, move focus to it
        If nextRow >= 0 Then
            Dim nextControl = g.MV1(nextRow).tb
            If nextControl IsNot Nothing Then
                nextControl.Focus()
                nextControl.SelectAll()
            End If
        End If
    End Sub

    ' === MV helpers (no duplicate reading) =======================================
    Private Function NormalizeNumericText(s As String) As String
        If String.IsNullOrWhiteSpace(s) Then Return ""
        Dim t = s.Trim()
        t = t.Replace(",", ".")
        ' keep leading sign; strip spaces
        t = System.Text.RegularExpressions.Regex.Replace(t, "\s+", "")
        Return t
    End Function

    Private Function ValuesEqual(a As String, b As String) As Boolean
        a = NormalizeNumericText(a) : b = NormalizeNumericText(b)
        If a = "" OrElse b = "" Then Return False
        Dim da As Double, db As Double
        If Double.TryParse(a, Globalization.NumberStyles.Any, Globalization.CultureInfo.InvariantCulture, da) AndAlso
       Double.TryParse(b, Globalization.NumberStyles.Any, Globalization.CultureInfo.InvariantCulture, db) Then
            Return Math.Abs(da - db) <= 0.0000001 ' numeric compare with tiny tolerance
        End If
        Return String.Equals(a, b, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function NextEmptySlot(g As Object, r As Integer) As Integer
        Dim grp = DirectCast(g, Object)
        Dim p = DirectCast(g, Object)
        Dim mg = DirectCast(g, Object)

        Dim s1 As TextBox = If(DirectCast(g, ParamGroup).MV1 IsNot Nothing AndAlso r < DirectCast(g, ParamGroup).MV1.Length, DirectCast(g, ParamGroup).MV1(r).tb, Nothing)
        Dim s2 As TextBox = If(DirectCast(g, ParamGroup).MV2 IsNot Nothing AndAlso r < DirectCast(g, ParamGroup).MV2.Length, DirectCast(g, ParamGroup).MV2(r).tb, Nothing)
        Dim s3 As TextBox = If(DirectCast(g, ParamGroup).MV3 IsNot Nothing AndAlso r < DirectCast(g, ParamGroup).MV3.Length, DirectCast(g, ParamGroup).MV3(r).tb, Nothing)
        If s1 IsNot Nothing AndAlso String.IsNullOrWhiteSpace(s1.Text) Then Return 1
        If s2 IsNot Nothing AndAlso String.IsNullOrWhiteSpace(s2.Text) Then Return 2
        If s3 IsNot Nothing AndAlso String.IsNullOrWhiteSpace(s3.Text) Then Return 3
        Return 0
    End Function

    Private Function GetSlotText(g As ParamGroup, r As Integer, slot As Integer) As String
        Select Case slot
            Case 1 : If g.MV1 IsNot Nothing AndAlso r < g.MV1.Length AndAlso g.MV1(r).tb IsNot Nothing Then Return g.MV1(r).tb.Text
            Case 2 : If g.MV2 IsNot Nothing AndAlso r < g.MV2.Length AndAlso g.MV2(r).tb IsNot Nothing Then Return g.MV2(r).tb.Text
            Case 3 : If g.MV3 IsNot Nothing AndAlso r < g.MV3.Length AndAlso g.MV3(r).tb IsNot Nothing Then Return g.MV3(r).tb.Text
        End Select
        Return ""
    End Function

    Private Sub SetSlotText(g As ParamGroup, r As Integer, slot As Integer, val As String)
        Select Case slot
            Case 1 : If g.MV1 IsNot Nothing AndAlso r < g.MV1.Length AndAlso g.MV1(r).tb IsNot Nothing Then g.MV1(r).tb.Text = val
            Case 2 : If g.MV2 IsNot Nothing AndAlso r < g.MV2.Length AndAlso g.MV2(r).tb IsNot Nothing Then g.MV2(r).tb.Text = val
            Case 3 : If g.MV3 IsNot Nothing AndAlso r < g.MV3.Length AndAlso g.MV3(r).tb IsNot Nothing Then g.MV3(r).tb.Text = val
        End Select
    End Sub

    Private Function IsDuplicateReadingInRow(g As ParamGroup, r As Integer, val As String) As Boolean
        Dim v = NormalizeNumericText(val)
        If v = "" Then Return False
        Dim t1 = If(g.MV1 IsNot Nothing AndAlso r < g.MV1.Length AndAlso g.MV1(r).tb IsNot Nothing, g.MV1(r).tb.Text, "")
        Dim t2 = If(g.MV2 IsNot Nothing AndAlso r < g.MV2.Length AndAlso g.MV2(r).tb IsNot Nothing, g.MV2(r).tb.Text, "")
        Dim t3 = If(g.MV3 IsNot Nothing AndAlso r < g.MV3.Length AndAlso g.MV3(r).tb IsNot Nothing, g.MV3(r).tb.Text, "")
        Return ValuesEqual(v, t1) OrElse ValuesEqual(v, t2) OrElse ValuesEqual(v, t3)
    End Function

    ' ============================================================================
    Private Sub ApplyReadingWithClickInterval(val As String)
        Dim gObj As ParamGroup = Nothing
        Dim r As Integer = -1
        'If Not ResolveOrGuessCurrentTarget(gObj, r) Then Exit Sub
        Dim grp = DirectCast(gObj, ParamGroup)
        Dim now As DateTime = DateTime.Now

        ' Decide which slot we *intend* to write next
        Dim slot As Integer = NextEmptySlot(grp, r)
        If slot = 0 Then
            ' row already complete; compute (safety) and bail
            If IsRowComplete(grp, r) Then
                currentGroup = grp : currentRowIdx = r
                currentExcelRow = GetRowFromAddr(grp.MV3(r).cell)
                ctxDc.TargetRow = currentExcelRow
                StartRowCompute(grp, r)
            End If
            Return
        End If

        ' === NO-DUPLICATE RULE ===
        If IsDuplicateReadingInRow(grp, r, val) Then
            MessageBox.Show("Same reading detected in this row. Please recapture a new image/reading.",
                        "Duplicate reading", MessageBoxButtons.OK, MessageBoxIcon.Information)
            ' Do NOT fill anything; user re-captures.
            Exit Sub
        End If

        ' Write the value into the intended slot
        SetSlotText(grp, r, slot, val)

        ' If the row is now complete → compute and advance
        If IsRowComplete(grp, r) Then
            currentGroup = grp
            currentRowIdx = r
            currentExcelRow = GetRowFromAddr(grp.MV3(r).cell)
            ctxDc.TargetRow = currentExcelRow
            StartRowCompute(grp, r)

            ' Reset click-interval state (fresh next row)
            lastClickGroup = Nothing
            lastClickRow = -1
            lastNextSlot = 1
        Else
            ' Keep state so next capture within 2s goes to the next slot
            lastClickGroup = grp
            lastClickRow = r
            lastNextSlot = slot + 1
            If lastNextSlot > 3 Then lastNextSlot = 3
        End If

        lastCaptureAt = now
    End Sub

    ' In your form's constructor or Load event, subscribe to the Enter event of each TextBox
    Private Sub SetupTextBoxEvents()
        ' Add the Enter event for all textboxes dynamically
        For Each ctrl As WinForms.Control In Me.Controls
            If TypeOf ctrl Is TextBox Then
                AddHandler ctrl.Enter, AddressOf TextBox_Enter
            End If
        Next
    End Sub

    ' Global variable to track focused textbox
    Private currentFocusedTextBox As TextBox

    ' Event handler to track focus change
    Private Sub TextBox_Enter(sender As Object, e As EventArgs)
        currentFocusedTextBox = CType(sender, TextBox)
    End Sub

    ' Mirror the template’s current cell values into the preview controls.
    ' It does NOT write anything; it only reads.
    Private Sub RefreshPreviewFromTemplate(Optional onlyGroup As ParamGroup = Nothing, Optional onlyRow As Integer = -1)
        If ctxDc Is Nothing Then Exit Sub

        Dim prevPre As Action(Of Object) = ctxDc.PreCalculate
        Dim prevPost As Action(Of Object) = ctxDc.AfterCalculate

        ' We won’t push anything; we only read.
        ctxDc.PreCalculate = Sub(ws As Object)
                                 ' no-op
                             End Sub

        ctxDc.AfterCalculate = Sub(ws As Object)
                                   Dim groupsToRead As IEnumerable(Of ParamGroup) =
            If(onlyGroup Is Nothing, Groups.Values, {onlyGroup})

                                   For Each g In groupsToRead
                                       If g Is Nothing Then Continue For

                                       ' Decide which rows to read
                                       Dim rowCount As Integer = 0
                                       rowCount = Math.Max(rowCount, If(g.MV1?.Length, 0))
                                       rowCount = Math.Max(rowCount, If(g.MV2?.Length, 0))
                                       rowCount = Math.Max(rowCount, If(g.MV3?.Length, 0))
                                       rowCount = Math.Max(rowCount, If(g.Tolerance?.Length, 0))
                                       rowCount = Math.Max(rowCount, If(g.UpperLimit?.Length, 0))
                                       rowCount = Math.Max(rowCount, If(g.LowerLimit?.Length, 0))
                                       rowCount = Math.Max(rowCount, If(g.Remarks?.Length, 0))
                                       rowCount = Math.Max(rowCount, If(g.Average?.Length, 0))
                                       rowCount = Math.Max(rowCount, If(g.Error?.Length, 0))
                                       rowCount = Math.Max(rowCount, If(g.FinalUncDecl?.Length, 0))
                                       If rowCount <= 0 Then Continue For

                                       Dim startIdx As Integer = If(onlyRow >= 0, onlyRow, 0)
                                       Dim endIdx As Integer = If(onlyRow >= 0, onlyRow, rowCount - 1)

                                       ' Inside RefreshPreviewFromTemplate(...) -> ctxDc.AfterCalculate
                                       For i As Integer = startIdx To endIdx
                                           ' existing input reads...
                                           If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length AndAlso g.MV1(i).tb IsNot Nothing Then
                                               g.MV1(i).tb.Text = SafeReadCell(ws, g.MV1(i).cell)
                                           End If
                                           If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length AndAlso g.MV2(i).tb IsNot Nothing Then
                                               g.MV2(i).tb.Text = SafeReadCell(ws, g.MV2(i).cell)
                                           End If
                                           If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length AndAlso g.MV3(i).tb IsNot Nothing Then
                                               g.MV3(i).tb.Text = SafeReadCell(ws, g.MV3(i).cell)
                                           End If

                                           ' existing outputs/limits:
                                           ReadOutputsRow(ws, g, i)

                                           ' >>> NEW: Fill the descriptor labels from Excel <<<
                                           ReadDescriptorRow(ws, g, i)
                                       Next
                                   Next
                               End Sub

        Try
            ' We can point TargetRow anywhere; AfterCalculate will iterate all needed rows.
            If currentExcelRow > 0 Then ctxDc.TargetRow = currentExcelRow
            CalRowModule.RecalculateNow(ctxDc)
        Finally
            ctxDc.PreCalculate = prevPre
            ctxDc.AfterCalculate = prevPost
        End Try
    End Sub

#End Region

End Class