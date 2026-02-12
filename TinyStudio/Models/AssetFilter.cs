using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TinyStudio.Models;

public class AssetFilter
{
    private HashSet<string> _selectedTypes = new();
    private bool _selectAll = true;
    private string _searchText = string.Empty;
    private bool _useRegex;
    private Regex? _regex;
    private string _lastPattern = string.Empty;
    
    public void UpdateTypeFilter(HashSet<string> selectedTypes, bool selectAll)
    {
        _selectedTypes = selectedTypes;
        _selectAll = selectAll;
    }

    public void UpdateSearchFilter(string searchText, bool useRegex)
    {
        if (_searchText == searchText && _useRegex == useRegex)
            return;

        _searchText = searchText;
        _useRegex = useRegex;

        if (string.IsNullOrEmpty(searchText))
        {
            _regex = null;
            return;
        }

        var pattern = useRegex ? searchText : Regex.Escape(searchText);

        if (_lastPattern != pattern)
        {
            try
            {
                _regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                _lastPattern = pattern;
            }
            catch (RegexParseException)
            {
                _regex = null;
                _lastPattern = pattern;
            }
        }
    }
    
    public bool Matches(AssetWrapper asset)
    {
        if (!_selectAll && _selectedTypes.Any() && !_selectedTypes.Contains(asset.Type))
            return false;

        if (_regex == null)
            return true;
        
        return _regex.IsMatch(asset.Name) ||
               _regex.IsMatch(asset.Container) ||
               _regex.IsMatch(asset.Type) ||
               _regex.IsMatch(asset.PathIdStr) ||
               _regex.IsMatch(asset.SizeStr);
    }
    
    public void Reset()
    {
        _selectAll = true;
        _selectedTypes = new();
        _regex = null;
    }
}