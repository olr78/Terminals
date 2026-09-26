using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Terminals.Localization
{
    /// <summary>
    /// Resource manager, which translates all string resources by the <see cref="Translator"/>.
    /// </summary>
    public class TranslatingResourceManager : ResourceManager
    {
        public TranslatingResourceManager(string baseName, Assembly assembly)
            : base(baseName, assembly)
        {
        }

        public override string GetString(string name, CultureInfo culture)
        {
            return Translator.T(base.GetString(name, culture));
        }
    }
}
