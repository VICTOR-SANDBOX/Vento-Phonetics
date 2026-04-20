using System.Collections.Generic;
using System.Linq;
using OpenUtau.Api;
using OpenUtau.Core.G2p;

namespace OpenUtau.Plugin.Builtin;

[Phonemizer("Portuguese CVVX Phonemizer", "PT-BR CVVX", "Vento", "PT")]
public class PortugueseCVVXPhonemizer : SyllableBasedPhonemizer
{
	private class PhoneticG2p : IG2p
	{
		private readonly string[] validSymbols;

		public PhoneticG2p(string[] validSymbols)
		{
			this.validSymbols = validSymbols.OrderByDescending((string s) => s.Length).ToArray();
		}

		public bool IsValidSymbol(string symbol) => validSymbols.Contains(symbol);

		public bool IsVowel(string symbol) => false;

		public bool IsGlide(string symbol) => false;

		public IList<string> Predict(string text) => Query(text);

		public string[] Query(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return new string[0];
			}
            
			List<string> list = new List<string>();
			string text2 = text;
            
			while (text2.Length > 0)
			{
				bool matchFound = false;
				foreach (string symbol in validSymbols)
				{
					if (text2.StartsWith(symbol))
					{
						list.Add(symbol);
						text2 = text2.Substring(symbol.Length);
						matchFound = true;
						break;
					}
				}
                
				if (!matchFound)
				{
					text2 = text2.Substring(1);
				}
			}
			return list.ToArray();
		}

		public string[] UnpackHint(string hint, char separator = ' ') => hint.Split(separator);
	}

	private readonly string[] vowels = "a,e,i,o,u,@,7,1,0,Q,X,V".Split(",");
	private readonly string[] consonants = "p,b,t,d,k,g,f,v,s,z,x,j,m,n,nh,l,lh,r,rr".Split(",");
	private readonly Dictionary<string, string> dictionaryReplacements = new Dictionary<string, string>();

	public PortugueseCVVXPhonemizer()
	{
		var rawReplacements = new Dictionary<string, string[]>
		{
			{ "@", new[] { "an", "am", "A", "ã", "a~" } },
			{ "7", new[] { "en", "em", "E", "ẽ", "e~" } },
			{ "1", new[] { "in", "im", "I", "ĩ", "i~" } },
			{ "0", new[] { "on", "om", "O", "õ", "o~" } },
			{ "Q", new[] { "un", "um", "U", "ũ", "u~" } },
			{ "X", new[] { "eh", "é" } },
			{ "V", new[] { "oh", "ó" } },
			{ "x", new[] { "ch", "sh", "S", "Ch", "tch", "tS", "T" } },
            { "d", new[] { "dj", "dy", "dZ" } },
            { "t", new[] { "ty" } },
			{ "rr", new[] { "h", "rh" } },
			{ "k", new[] { "q", "qu", "c" } },
			{ "j", new[] { "zh", "Z", "jh" } },
			{ "nh", new[] { "ñ", "J" } },
            { "u", new[] { "w" } },
            { "i", new[] { "y" } }
		};

		foreach (var kvp in rawReplacements)
		{
			foreach (string alias in kvp.Value)
			{
				if (!dictionaryReplacements.ContainsKey(alias))
				{
					dictionaryReplacements.Add(alias, kvp.Key);
				}
			}
		}
	}

	protected override string[] GetVowels() => vowels;
	protected override string[] GetConsonants() => consonants;
	protected override string GetDictionaryName() => "";
	protected override Dictionary<string, string> GetDictionaryPhonemesReplacement() => dictionaryReplacements;

	protected override IG2p LoadBaseDictionary()
	{
		return new PhoneticG2p(vowels.Concat(consonants).Concat(dictionaryReplacements.Keys).ToArray());
	}

	protected override List<string> ProcessSyllable(Syllable syllable)
	{
		string prevV = syllable.prevV;
		string[] cc = syllable.cc;
		string v = syllable.v;
		List<string> list = new List<string>();

		if (syllable.IsStartingV)
		{
			list.Add($"- {v}");
		}
		else if (syllable.IsVV)
		{
			if (!CanMakeAliasExtension(syllable))
			{
                HandleVV(prevV, v, syllable.vowelTone, list);
			}
		}
		else if (syllable.IsStartingCVWithOneConsonant || syllable.IsStartingCVWithMoreThanOneConsonant)
		{
            list.Add($"{cc.Last()}{v}");
		}
		else
		{
            HandleVC(prevV, cc, v, syllable.tone, list);
		}

		return list;
	}

    private void HandleVV(string prevV, string v, int tone, List<string> list)
    {
        string text = $"{prevV} {v}";
        if (HasOto(text, tone))
        {
            list.Add(text);
        }
        else if (v == "u")
        {
            list.Add($"{prevV} w");
        }
        else if (v == "i")
        {
            list.Add($"{prevV} y");
        }
        else
        {
            list.Add(v); // fallback se n tiver VV
        }
    }

    private void HandleVC(string prevV, string[] cc, string v, int tone, List<string> list)
    {
        string vc = $"{prevV} {cc[0]}";
        if (HasOto(vc, tone))
        {
            list.Add(vc);
        }

        string cv = $"{cc.Last()}{v}";
        list.Add(cv);
    }

	protected override List<string> ProcessEnding(Ending ending)
	{
		List<string> list = new List<string>();

		if (ending.IsEndingV)
		{
            // Note that has no rest behind it, just sustain
            return list;
		}

        string v_dash = $"{ending.prevV} -";
        if (HasOto(v_dash, ending.tone))
        {
            list.Add(v_dash);
        }

		return list;
	}

	protected override double GetTransitionBasicLengthMs(string alias = "")
	{
		if (alias.StartsWith("r") || alias.EndsWith(" r"))
		{
			return base.GetTransitionBasicLengthMs() * 0.50;
		}

		return base.GetTransitionBasicLengthMs();
	}
}
