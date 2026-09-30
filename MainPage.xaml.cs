using System.Globalization;

namespace Calculatrice;

public partial class MainPage : ContentPage
{
    private const int MaxDigits = 15;

    private string _current = "0";      // nombre en cours de saisie / résultat affiché
    private double? _left;              // premier opérande
    private string? _op;                // opérateur en attente (+ − × ÷)
    private string _expression = "";    // texte affiché au-dessus du résultat
    private bool _newEntry = true;      // le prochain chiffre démarre un nouveau nombre
    private bool _error;                // état d'erreur (ex. division par zéro)
    private double _baseFontSize = 56;  // taille de police selon l'orientation

    public MainPage()
    {
        InitializeComponent();

        // Le contenu du ScrollView prend au minimum toute la largeur visible :
        // le texte reste donc aligné à droite tant qu'il ne déborde pas.
        DisplayScroll.SizeChanged += (_, _) =>
            DisplayStack.MinimumWidthRequest = DisplayScroll.Width;
    }

    // Adaptation au changement d'orientation / à la taille de l'écran
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0) return;

        bool paysage = width > height;
        double nouvelleTaille = paysage ? 36 : 56;
        if (Math.Abs(nouvelleTaille - _baseFontSize) > 0.1)
        {
            _baseFontSize = nouvelleTaille;
            RefreshDisplay();
        }
    }

    // ---------- Gestionnaires d'événements ----------

    private void OnDigit(object? sender, EventArgs e)
    {
        if (sender is not Button b) return;
        if (_error) ResetAll();

        string digit = b.Text;

        if (_newEntry)
        {
            // Nouveau nombre après "=" : on efface l'ancienne opération affichée
            if (_op == null) _expression = "";
            _current = digit;
            _newEntry = false;
        }
        else if (_current == "0")
        {
            _current = digit;
        }
        else if (_current == "-0")
        {
            _current = "-" + digit;
        }
        else if (CountDigits(_current) < MaxDigits)
        {
            _current += digit;
        }

        RefreshDisplay();
    }

    private void OnDecimal(object? sender, EventArgs e)
    {
        if (_error) ResetAll();

        if (_newEntry)
        {
            if (_op == null) _expression = "";
            _current = "0.";
            _newEntry = false;
        }
        else if (!_current.Contains('.') && CountDigits(_current) < MaxDigits)
        {
            _current += ".";
        }

        RefreshDisplay();
    }

    private void OnOperator(object? sender, EventArgs e)
    {
        if (_error || sender is not Button b) return;
        string newOp = b.Text;

        if (_op != null && _newEntry)
        {
            // L'utilisateur change d'avis : on remplace simplement l'opérateur
            _op = newOp;
            _expression = $"{Format(_left!.Value)} {_op}";
            RefreshDisplay();
            return;
        }

        double value = Parse(_current);

        if (_op != null && _left != null)
        {
            // Enchaînement : 2 + 3 × ... => on calcule d'abord 2 + 3
            if (!TryCompute(_left.Value, value, _op, out double result))
            {
                ShowError($"{Format(_left.Value)} {_op} {Format(value)}");
                return;
            }
            _left = result;
            _current = Format(result);
        }
        else
        {
            _left = value;
        }

        _op = newOp;
        _expression = $"{Format(_left.Value)} {_op}";
        _newEntry = true;
        RefreshDisplay();
    }

    private void OnEquals(object? sender, EventArgs e)
    {
        if (_error || _op == null || _left == null) return;

        double right = Parse(_current);
        string exprText = $"{Format(_left.Value)} {_op} {Format(right)} =";

        if (!TryCompute(_left.Value, right, _op, out double result))
        {
            ShowError(exprText);
            return;
        }

        _current = Format(result);
        _expression = exprText;
        _left = null;
        _op = null;
        _newEntry = true;
        RefreshDisplay();
    }

    private void OnClear(object? sender, EventArgs e)
    {
        ResetAll();
        RefreshDisplay();
    }

    private void OnBackspace(object? sender, EventArgs e)
    {
        if (_error) { ResetAll(); RefreshDisplay(); return; }
        if (_newEntry) return; // on n'efface pas un résultat calculé

        _current = _current.Length > 1 ? _current[..^1] : "0";
        if (_current == "-" || _current == "") _current = "0";
        RefreshDisplay();
    }

    private void OnSign(object? sender, EventArgs e)
    {
        if (_error) return;
        if (_current == "0") return;

        _current = _current.StartsWith('-') ? _current[1..] : "-" + _current;
        RefreshDisplay();
    }

    private void OnPercent(object? sender, EventArgs e)
    {
        if (_error) return;

        double value = Parse(_current);

        // "200 + 10 %" => 10 % de 200 = 20 ; sinon simple division par 100
        value = (_op != null && _left != null)
            ? _left.Value * value / 100
            : value / 100;

        _current = Format(value);
        _newEntry = false;
        RefreshDisplay();
    }

    // ---------- Logique de calcul ----------

    private static bool TryCompute(double a, double b, string op, out double result)
    {
        result = 0;
        switch (op)
        {
            case "+": result = a + b; break;
            case "−": result = a - b; break;
            case "×": result = a * b; break;
            case "÷":
                if (b == 0) return false; // division par zéro : pas de plantage
                result = a / b;
                break;
            default: return false;
        }
        return !double.IsNaN(result) && !double.IsInfinity(result);
    }

    private void ShowError(string context)
    {
        _error = true;
        _current = "Erreur";
        _expression = context.Contains('÷')
            ? "Division par zéro impossible"
            : "Résultat hors limites";
        _left = null;
        _op = null;
        _newEntry = true;
        RefreshDisplay();
    }

    private void ResetAll()
    {
        _current = "0";
        _left = null;
        _op = null;
        _expression = "";
        _newEntry = true;
        _error = false;
    }

    // ---------- Utilitaires ----------

    private static double Parse(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;

    // G15 évite les artefacts binaires : 0.1 + 0.2 => 0.3
    private static string Format(double v) =>
        v.ToString("G15", CultureInfo.InvariantCulture);

    private static int CountDigits(string s) => s.Count(char.IsDigit);

    private async void RefreshDisplay()
    {
        ResultLabel.Text = _current;
        ExpressionLabel.Text = _expression;

        // Réduit la police quand le nombre est long
        int len = _current.Length;
        double factor = len <= 9 ? 1.0 : len <= 12 ? 0.75 : 0.6;
        ResultLabel.FontSize = _baseFontSize * factor;
        ExpressionLabel.FontSize = _baseFontSize * 0.4;

        // Fait défiler vers la droite (dernier caractère saisi visible)
        await Task.Yield();
        await DisplayScroll.ScrollToAsync(DisplayStack.Width, 0, false);
    }
}
