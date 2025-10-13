Imports System.Collections
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports System.Text
Imports System.Windows.Forms

Module CalibratingResultMappings

    ''=========================
    '' Internal data model (names aligned with ParamGroup)
    ''=========================
    'Private Class Block
    '    Public Key As String
    '    Public StartRow As Integer

    '    ' descriptors
    '    Public COL_FUNCTION As String()

    '    Public RangeLabel As String()
    '    Public Nominal As String()
    '    Public Unit As String()
    '    Public Frequency As String()
    '    Public FreqUnit As String()

    '    ' inputs
    '    Public MV1 As String()

    '    Public MV2 As String()
    '    Public MV3 As String()

    '    ' outputs
    '    Public AVG As String()

    '    Public ERR As String()
    '    Public UNC As String()

    '    ' limits + remarks
    '    Public TOL As String()

    '    Public UP As String()
    '    Public LO As String()
    '    Public Remarks As String()
    'End Class

    <Extension()>
    Public Sub InitMappings(frm As calibratingResult)

        ' === Excel column letters (match your sheet) ===
        Const COL_FUNCTION As String = "A"
        Const COL_RANGE_LBL As String = "B"
        Const COL_NOMINAL As String = "C"
        Const COL_UNIT As String = "D"
        Const COL_FREQUENCY As String = "E"
        Const COL_FREQ_UNIT As String = "F"

        Const COL_MV1 As String = "G"
        Const COL_MV2 As String = "H"
        Const COL_MV3 As String = "I"
        Const COL_AVG As String = "J"
        Const COL_ERR As String = "K"
        Const COL_TOL As String = "N"
        Const COL_UP As String = "O"
        Const COL_LO As String = "P"
        Const COL_REM As String = "Q"
        Const COL_UNC As String = "AI"

        ' ---------- reflection ----------
        Dim paramGroupType As Type = frm.GetType().GetNestedType("ParamGroup", BindingFlags.NonPublic)
        If paramGroupType Is Nothing Then Throw New MissingMemberException("ParamGroup type not found on calibratingResult.")

        Dim fiGroups As FieldInfo = frm.GetType().GetField("Groups", BindingFlags.Instance Or BindingFlags.NonPublic)
        If fiGroups Is Nothing Then Throw New MissingMemberException("Groups field not found on calibratingResult.")
        Dim Groups As IDictionary = TryCast(fiGroups.GetValue(frm), IDictionary)
        If Groups Is Nothing Then Throw New InvalidOperationException("Groups is not a dictionary on calibratingResult.")

        ' ---------- sheet name + ctx instance (no ?. chain) ----------
        Dim sheetName As String = "DEFAULT"   ' safe default
        Dim ctx As Object = Nothing

        Try
            Dim ctxDcField As FieldInfo = frm.GetType().GetField("ctxDc", BindingFlags.Instance Or BindingFlags.NonPublic)
            If ctxDcField IsNot Nothing Then
                ctx = ctxDcField.GetValue(frm)           ' <-- this is the instance we must pass to WithWorksheet
                If ctx IsNot Nothing Then
                    Dim prop As PropertyInfo = ctx.GetType().GetProperty("SheetInputsName", BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic)
                    If prop IsNot Nothing Then
                        Dim val As Object = prop.GetValue(ctx, Nothing)
                        If val IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(val.ToString()) Then
                            sheetName = val.ToString()
                        End If
                    End If
                End If
            End If
        Catch
            ' keep default
        End Try

        If String.IsNullOrWhiteSpace(sheetName) Then sheetName = "DEFAULT"

        ' Make a unique key if needed to avoid overwriting another mapping
        Dim baseKey As String = sheetName
        Dim suffix As Integer = 1
        While Groups.Contains(baseKey)
            baseKey = sheetName & "_" & suffix.ToString()
            suffix += 1
        End While
        sheetName = baseKey

        ' ---------- resolve the sheet container to scope control lookups ----------
        Dim sheetScope As Control = Nothing

        ' 1) Try a control whose Name == sheetName
        Dim byName() As Control = frm.Controls.Find(sheetName, True)
        If byName IsNot Nothing AndAlso byName.Length > 0 Then
            sheetScope = byName(0)
        End If

        ' 2) Try a TabPage whose Text matches sheetName
        If sheetScope Is Nothing Then
            For Each tc As TabControl In frm.Controls.OfType(Of TabControl)()
                For Each tp As TabPage In tc.TabPages
                    If String.Equals(tp.Text, sheetName, StringComparison.OrdinalIgnoreCase) Then
                        sheetScope = tp
                        Exit For
                    End If
                Next
                If sheetScope IsNot Nothing Then Exit For
            Next
        End If

        ' 3) Fallback to the whole form
        If sheetScope Is Nothing Then sheetScope = frm

        ' ---------- helpers (SCOPED to the current sheet) ----------
        Dim FindLabel As Func(Of String, Label) =
            Function(name As String) As Label
                If String.IsNullOrEmpty(name) Then Return Nothing
                Dim arr() As Control = sheetScope.Controls.Find(name, True)
                If arr IsNot Nothing AndAlso arr.Length > 0 Then
                    Return TryCast(arr(0), Label)
                End If
                Return Nothing
            End Function

        Dim FindTextBox As Func(Of String, TextBox) =
            Function(name As String) As TextBox
                If String.IsNullOrEmpty(name) Then Return Nothing
                Dim arr() As Control = sheetScope.Controls.Find(name, True)
                If arr IsNot Nothing AndAlso arr.Length > 0 Then
                    Return TryCast(arr(0), TextBox)
                End If
                Return Nothing
            End Function

        Dim SetPgField As Action(Of Object, String, Object) =
            Sub(pgObj As Object, fieldName As String, value As Object)
                Dim fi As FieldInfo = pgObj.GetType().GetField(fieldName, BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.IgnoreCase)
                If fi Is Nothing Then Throw New MissingMemberException("ParamGroup is missing field '" & fieldName & "'.")
                fi.SetValue(pgObj, value)
            End Sub

        ' Build an array of names up to N, inserting Nothing where a control is missing — SCOPED
        Dim namesUpToN As Func(Of String, Integer, String()) =
            Function(baseName As String, maxCount As Integer) As String()
                Dim list As New List(Of String)()
                For i As Integer = 0 To maxCount - 1
                    Dim candidate As String = baseName & "_" & i.ToString()
                    Dim arr() As Control = sheetScope.Controls.Find(candidate, True)
                    list.Add(If(arr IsNot Nothing AndAlso arr.Length > 0, candidate, Nothing))
                Next
                Return list.ToArray()
            End Function

        ' tuple setters that ALWAYS allocate length=maxCount and ALWAYS assign a valid Excel address
        Dim setLBCellsN As Action(Of Object, String, String, Integer, String()) =
            Sub(pgObj As Object, fieldName As String, col As String, rowStart As Integer, namesArr As String())
                Dim len As Integer = If(namesArr Is Nothing, 0, namesArr.Length)
                Dim arr(If(len = 0, 0, len - 1)) As (Label, String)
                For i As Integer = 0 To len - 1
                    Dim lb As Label = FindLabel(namesArr(i))
                    Dim cellAddr As String = col & (rowStart + i).ToString()
                    arr(i) = (lb, cellAddr)
                Next
                SetPgField(pgObj, fieldName, arr)
            End Sub

        Dim setTBCellsN As Action(Of Object, String, String, Integer, String()) =
            Sub(pgObj As Object, fieldName As String, col As String, rowStart As Integer, namesArr As String())
                Dim len As Integer = If(namesArr Is Nothing, 0, namesArr.Length)
                Dim arr(If(len = 0, 0, len - 1)) As (TextBox, String)
                For i As Integer = 0 To len - 1
                    Dim tb As TextBox = FindTextBox(namesArr(i))
                    Dim cellAddr As String = col & (rowStart + i).ToString()
                    arr(i) = (tb, cellAddr)
                Next
                SetPgField(pgObj, fieldName, arr)
            End Sub

        ' ---------- determine max row count from TEMPLATE (Column A only) ----------
        Dim startRow As Integer = 2
        Dim maxScan As Integer = 5000
        Dim lastNonEmptyRow As Integer = 0
        Dim sawAny As Boolean = False
        Dim blankStreak As Integer = 0
        Const StopAfterBlanks As Integer = 200

        Try
            ' IMPORTANT: pass the ctx INSTANCE, not a FieldInfo
            CalRowModule.WithWorksheet(ctx, Sub(ws As Object)
                                                For r As Integer = startRow To startRow + maxScan - 1
                                                    Dim aVal = CalRowModule.ReadCell(ws, "A" & r)
                                                    Dim aTxt As String = If(aVal Is Nothing, "", CStr(aVal)).Trim()

                                                    If aTxt.Length > 0 Then
                                                        sawAny = True
                                                        lastNonEmptyRow = r
                                                        blankStreak = 0
                                                    ElseIf sawAny Then
                                                        blankStreak += 1
                                                        If blankStreak >= StopAfterBlanks Then Exit For
                                                    End If
                                                Next
                                            End Sub)
        Catch
            ' ignore and fall back to defaults
        End Try

        ' EXACT formula: actual last row minus (startRow - 1)
        Dim maxRows As Integer
        If lastNonEmptyRow >= startRow Then
            maxRows = lastNonEmptyRow - (startRow - 1)
        Else
            maxRows = 1
        End If

        ' ---------- build the names arrays using TEMPLATE row count ----------
        Dim rowCount As Integer = Math.Max(1, Math.Min(maxRows, 1000))  ' cap to keep UI snappy

        Dim names_COL_FUNCTION() As String = namesUpToN("COL_FUNCTION", rowCount)
        Dim names_RANGE() As String = namesUpToN("RANGE", rowCount)
        Dim names_NOM() As String = namesUpToN("NOM", rowCount)
        Dim names_UNIT2() As String = namesUpToN("UNIT2", rowCount)
        Dim names_FREQ() As String = namesUpToN("FREQ", rowCount)
        Dim names_UNIT() As String = namesUpToN("UNIT", rowCount)

        Dim names_MV1() As String = namesUpToN("MV1", rowCount)
        Dim names_MV2() As String = namesUpToN("MV2", rowCount)
        Dim names_MV3() As String = namesUpToN("MV3", rowCount)

        Dim names_AVG() As String = namesUpToN("AVG", rowCount)
        Dim names_ERR() As String = namesUpToN("ERR", rowCount)
        Dim names_UNC() As String = namesUpToN("UNC", rowCount)

        Dim names_TOL() As String = namesUpToN("TOL", rowCount)
        Dim names_UP() As String = namesUpToN("UP", rowCount)
        Dim names_LO() As String = namesUpToN("LO", rowCount)
        Dim names_REM() As String = namesUpToN("REM", rowCount)

        ' ---------- build ParamGroup and map ----------
        Dim pg As Object = Activator.CreateInstance(paramGroupType, True)

        ' Descriptors
        setLBCellsN(pg, "COL_FUNCTION", COL_FUNCTION, startRow, names_COL_FUNCTION)
        setLBCellsN(pg, "RangeLabel", COL_RANGE_LBL, startRow, names_RANGE)
        setLBCellsN(pg, "Nominal", COL_NOMINAL, startRow, names_NOM)
        setLBCellsN(pg, "Unit", COL_UNIT, startRow, names_UNIT2)
        setLBCellsN(pg, "Frequency", COL_FREQUENCY, startRow, names_FREQ)
        setLBCellsN(pg, "FreqUnit", COL_FREQ_UNIT, startRow, names_UNIT)

        ' Inputs
        setTBCellsN(pg, "MV1", COL_MV1, startRow, names_MV1)
        setTBCellsN(pg, "MV2", COL_MV2, startRow, names_MV2)
        setTBCellsN(pg, "MV3", COL_MV3, startRow, names_MV3)

        ' Outputs (labels)
        setLBCellsN(pg, "Average", COL_AVG, startRow, names_AVG)
        setLBCellsN(pg, "Error", COL_ERR, startRow, names_ERR)
        setLBCellsN(pg, "FinalUncDecl", COL_UNC, startRow, names_UNC)

        ' Limits / Remarks (textboxes)
        setTBCellsN(pg, "Tolerance", COL_TOL, startRow, names_TOL)
        setTBCellsN(pg, "UpperLimit", COL_UP, startRow, names_UP)
        setTBCellsN(pg, "LowerLimit", COL_LO, startRow, names_LO)
        setTBCellsN(pg, "Remarks", COL_REM, startRow, names_REM)

        SetPgField(pg, "TemplateRowCount", rowCount)

        Groups(sheetName) = pg
    End Sub

End Module