Imports System
Imports System.Linq
Imports System.Collections.Generic

Module excelCalculated

    '-------------------------
    ' Standard deviation (no Excel)
    '-------------------------
    Public Function StdDevPopulation(values As IEnumerable(Of Double)) As Double
        Dim n = values.Count()
        If n = 0 Then Throw New ArgumentException("values is empty.")
        Dim mean = values.Average()
        Dim sumSq = values.Sum(Function(v) (v - mean) * (v - mean))
        Return Math.Sqrt(sumSq / n)
    End Function

    Public Function StdDevSample(values As IEnumerable(Of Double)) As Double
        Dim n = values.Count()
        If n < 2 Then Throw New ArgumentException("need at least 2 values.")
        Dim mean = values.Average()
        Dim sumSq = values.Sum(Function(v) (v - mean) * (v - mean))
        Return Math.Sqrt(sumSq / (n - 1))
    End Function

    '-------------------------
    ' Excel’s TINV(two-tailed) equivalent
    ' t = inverse Student t for two-tailed probability p and df
    ' Result is positive t such that P(|T| >= t) = p
    '-------------------------
    Public Function TInvTwoTailed(p As Double, df As Integer) As Double
        If p <= 0 OrElse p >= 1 Then Throw New ArgumentOutOfRangeException(NameOf(p), "p must be in (0,1).")
        If df < 1 Then Throw New ArgumentOutOfRangeException(NameOf(df), "df must be >= 1.")
        ' Two-tailed p => upper-tail CDF target q = 1 - p/2
        Dim q As Double = 1.0 - p / 2.0
        Return InverseStudentTCdf(q, df)
    End Function

    Public Function RoundTInvTwoTailed(p As Double, df As Integer) As Double
        Dim t = TInvTwoTailed(p, df)
        ' Excel ROUND rounds halves away from zero
        Return Math.Round(t, 0, MidpointRounding.AwayFromZero)
    End Function

    '-------------------------
    ' Student-t CDF and inverse (no libraries)
    ' Uses regularized incomplete beta & bisection
    '-------------------------
    Private Function StudentTCdf(x As Double, v As Integer) As Double
        If x = 0 Then Return 0.5
        Dim t As Double = v / (v + x * x)
        Dim a As Double = v / 2.0
        Dim b As Double = 0.5
        Dim ib As Double = RegularizedIncompleteBeta(a, b, t)
        If x > 0 Then
            Return 1.0 - 0.5 * ib
        Else
            Return 0.5 * ib
        End If
    End Function

    Private Function InverseStudentTCdf(q As Double, v As Integer) As Double
        ' Find x such that CDF(x) = q, with x >= 0 when q >= 0.5
        If q <= 0 OrElse q >= 1 Then Throw New ArgumentOutOfRangeException(NameOf(q), "q must be in (0,1).")
        Dim target As Double = q
        ' Bracket
        Dim lo As Double = -1.0, hi As Double = 1.0
        While StudentTCdf(lo, v) > target : lo *= 2 : End While
        While StudentTCdf(hi, v) < target : hi *= 2 : End While
        ' Bisection
        For i = 1 To 200
            Dim mid = 0.5 * (lo + hi)
            Dim c = StudentTCdf(mid, v)
            If Math.Abs(c - target) < 0.0000000001 Then Return Math.Abs(mid)
            If c < target Then
                lo = mid
            Else
                hi = mid
            End If
        Next
        Return Math.Abs(0.5 * (lo + hi))
    End Function

    '-------------------------
    ' Regularized incomplete beta I_x(a,b)
    ' (Lanczos log-gamma + continued fraction)
    '-------------------------
    Private Function RegularizedIncompleteBeta(a As Double, b As Double, x As Double) As Double
        If x <= 0.0 Then Return 0.0
        If x >= 1.0 Then Return 1.0

        Dim bt As Double = Math.Exp(LogGamma(a + b) - LogGamma(a) - LogGamma(b) + a * Math.Log(x) + b * Math.Log(1.0 - x))

        Dim result As Double
        If x < (a + 1.0) / (a + b + 2.0) Then
            result = bt * BetaContinuedFraction(a, b, x) / a
        Else
            result = 1.0 - bt * BetaContinuedFraction(b, a, 1.0 - x) / b
        End If
        Return result
    End Function

    Private Function BetaContinuedFraction(a As Double, b As Double, x As Double) As Double
        Const MAXIT As Integer = 200
        Const EPS As Double = 0.00000000000003
        Const FPMIN As Double = 1.0E-300

        Dim qab = a + b
        Dim qap = a + 1.0
        Dim qam = a - 1.0

        Dim c As Double = 1.0
        Dim d As Double = 1.0 - qab * x / qap
        If Math.Abs(d) < FPMIN Then d = FPMIN
        d = 1.0 / d
        Dim h As Double = d

        For m As Integer = 1 To MAXIT
            Dim m2 = 2 * m

            Dim aa = m * (b - m) * x / ((qam + m2) * (a + m2))
            d = 1.0 + aa * d : If Math.Abs(d) < FPMIN Then d = FPMIN
            c = 1.0 + aa / c : If Math.Abs(c) < FPMIN Then c = FPMIN
            d = 1.0 / d
            h *= d * c

            aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2))
            d = 1.0 + aa * d : If Math.Abs(d) < FPMIN Then d = FPMIN
            c = 1.0 + aa / c : If Math.Abs(c) < FPMIN Then c = FPMIN
            d = 1.0 / d
            Dim del = d * c
            h *= del

            If Math.Abs(del - 1.0) < EPS Then Exit For
        Next
        Return h
    End Function

    ' Lanczos log-gamma
    Private Function LogGamma(z As Double) As Double
        Dim g As Double = 7
        Dim p As Double() = {
            0.99999999999980993,
            676.5203681218851,
            -1259.1392167224028,
            771.32342877765313,
            -176.61502916214059,
            12.507343278686905,
            -0.13857109526572012,
            0.0000099843695780195716,
            0.00000015056327351493116
        }

        If z < 0.5 Then
            Return Math.Log(Math.PI) - Math.Log(Math.Sin(Math.PI * z)) - LogGamma(1 - z)
        Else
            z -= 1.0
            Dim x = p(0)
            For i = 1 To 8
                x += p(i) / (z + i)
            Next
            Dim t = z + g + 0.5
            Return 0.5 * Math.Log(2 * Math.PI) + (z + 0.5) * Math.Log(t) - t + Math.Log(x)
        End If
    End Function

End Module