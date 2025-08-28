' defaultParameters.vb
Imports System.Text.RegularExpressions

Module defaultParameters

    ' range, nominal (as text), frequency
    Public ReadOnly Parameters As New Dictionary(Of String, List(Of Tuple(Of String, String, String))) From {
        {"DC Voltage Test", New List(Of Tuple(Of String, String, String)) From {
            Tuple.Create("600 mV", "60", "-"),
            Tuple.Create("600 mV", "540", "-"),
            Tuple.Create("600 mV", "-540", "-"),
            Tuple.Create("6 V", "0.6", "-"),
            Tuple.Create("6 V", "5.4", "-"),
            Tuple.Create("6 V", "-5.4", "-"),
            Tuple.Create("60 V", "6", "-"),
            Tuple.Create("60 V", "30", "-"),
            Tuple.Create("60 V", "54", "-"),
            Tuple.Create("60 V", "-6", "-"),
            Tuple.Create("60 V", "-54", "-"),
            Tuple.Create("600 V", "60", "-"),
            Tuple.Create("600 V", "540", "-"),
            Tuple.Create("600 V", "-540", "-"),
            Tuple.Create("1000 V", "100", "-"),
            Tuple.Create("1000 V", "900", "-"),
            Tuple.Create("1000 V", "-900", "-")
        }},
        {"AC Voltage Test", New List(Of Tuple(Of String, String, String)) From {
            Tuple.Create("600 mV", "60", "50 Hz"),
            Tuple.Create("600 mV", "60", "1 kHz"),
            Tuple.Create("600 mV", "540", "50 Hz"),
            Tuple.Create("600 mV", "540", "1 kHz"),
            Tuple.Create("6 V", "0.6", "50 Hz"),
            Tuple.Create("6 V", "0.6", "1 kHz"),
            Tuple.Create("6 V", "5.4", "50 Hz"),
            Tuple.Create("6 V", "5.4", "1 kHz"),
            Tuple.Create("60 V", "6", "50 Hz"),
            Tuple.Create("60 V", "6", "1 kHz"),
            Tuple.Create("60 V", "30", "50 Hz"),
            Tuple.Create("60 V", "30", "1 kHz"),
            Tuple.Create("60 V", "54", "50 Hz"),
            Tuple.Create("60 V", "54", "1 kHz"),
            Tuple.Create("600 V", "60", "50 Hz"),
            Tuple.Create("600 V", "60", "1 kHz"),
            Tuple.Create("600 V", "540", "50 Hz"),
            Tuple.Create("600 V", "540", "1 kHz"),
            Tuple.Create("1000 V", "100", "50 Hz"),
            Tuple.Create("1000 V", "100", "1 kHz"),
            Tuple.Create("1000 V", "900", "50 Hz"),
            Tuple.Create("1000 V", "900", "1 kHz")
        }},
        {"Resistance Test", New List(Of Tuple(Of String, String, String)) From {
            Tuple.Create("600 Ω", "0", "-"),
            Tuple.Create("600 Ω", "540", "-"),
            Tuple.Create("6 kΩ", "5.4", "-"),
            Tuple.Create("60 kΩ", "54", "-"),
            Tuple.Create("600 kΩ", "540", "-"),
            Tuple.Create("6 MΩ", "5.4", "-"),
            Tuple.Create("50 MΩ", "45", "-")
        }},
        {"DC Current Test", New List(Of Tuple(Of String, String, String)) From {
            Tuple.Create("600 µA", "540", "-"),
            Tuple.Create("6000 µA", "5400", "-"),
            Tuple.Create("60 mA", "54", "-"),
            Tuple.Create("60 mA", "-54", "-"),
            Tuple.Create("400 mA", "360", "-"),
            Tuple.Create("6 A", "3", "-"),
            Tuple.Create("6 A", "5.4", "-"),
            Tuple.Create("10 A", "5", "-"),
            Tuple.Create("10 A", "9", "-")
        }},
        {"AC Current Test", New List(Of Tuple(Of String, String, String)) From {
            Tuple.Create("600 µA", "540", "50 Hz"),
            Tuple.Create("600 µA", "540", "1 kHz"),
            Tuple.Create("6000 µA", "5400", "50 Hz"),
            Tuple.Create("6000 µA", "5400", "1 kHz"),
            Tuple.Create("60 mA", "54", "50 Hz"),
            Tuple.Create("60 mA", "54", "1 kHz"),
            Tuple.Create("400 mA", "360", "50 Hz"),
            Tuple.Create("400 mA", "360", "1 kHz"),
            Tuple.Create("6 A", "5.4", "50 Hz"),
            Tuple.Create("6 A", "5.4", "1 kHz"),
            Tuple.Create("10 A", "9", "50 Hz"),
            Tuple.Create("10 A", "9", "1 kHz")
        }}
    }

    ' Public: returns rows with nominal already suffixed with the range unit
    Public Function GetFormattedParameters() As Dictionary(Of String, List(Of Tuple(Of String, String, String)))
        Dim result As New Dictionary(Of String, List(Of Tuple(Of String, String, String)))()
        For Each kvp In Parameters
            Dim list As New List(Of Tuple(Of String, String, String))()
            For Each row In kvp.Value
                Dim rng As String = row.Item1
                Dim nominalRaw As String = row.Item2
                Dim freq As String = row.Item3
                Dim nominalFmt As String = FormatNominalWithRangeUnit(nominalRaw, rng)
                list.Add(Tuple.Create(rng, nominalFmt, freq))
            Next
            result.Add(kvp.Key, list)
        Next
        Return result
    End Function

    Private Function ExtractUnitFromRange(rangeText As String) As String
        If String.IsNullOrWhiteSpace(rangeText) Then Return ""
        Dim parts = rangeText.Trim().Split({" "c, vbTab}, StringSplitOptions.RemoveEmptyEntries)
        For i = parts.Length - 1 To 0 Step -1
            If Regex.IsMatch(parts(i), "[A-Za-zµΩΩ]") Then
                Return parts(i).Replace("Ω", "Ω")
            End If
        Next
        Return ""
    End Function

    Private Function FormatNominalWithRangeUnit(nominalValue As String, rangeText As String) As String
        Dim unit As String = ExtractUnitFromRange(rangeText)
        If String.IsNullOrWhiteSpace(nominalValue) Then Return nominalValue
        If Regex.IsMatch(nominalValue, "[A-Za-zµΩΩ]\s*$") Then Return nominalValue
        If unit = "" Then Return nominalValue
        Return nominalValue.Trim() & " " & unit
    End Function

End Module