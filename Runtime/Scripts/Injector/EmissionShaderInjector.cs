#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Shell.Protector
{
    public static class EmissionShaderInjector
    {
        // Parse balanced arguments: UV expressions often contain nested calls and commas.
        public static string InjectPoiyomi(string code)
        {
            var calls = Regex.Matches(code, @"\b(POI2D_SAMPLER_PAN|UNITY_SAMPLE_TEX2D_SAMPLER)\s*\(\s*(_EmissionMap[123]?)\s*,");
            for (int i = calls.Count - 1; i >= 0; i--)
            {
                Match match = calls[i];
                int open = code.IndexOf('(', match.Index);
                int depth = 1, start = open + 1, end = start;
                var args = new List<string>();
                for (; end < code.Length && depth > 0; end++)
                {
                    char c = code[end];
                    if (c == '(') depth++;
                    if (c == ')') depth--;
                    if ((c == ',' && depth == 1) || depth == 0)
                    {
                        args.Add(code.Substring(start, end - start).Trim());
                        start = end + 1;
                    }
                }
                bool pan = match.Groups[1].Value == "POI2D_SAMPLER_PAN";
                if (depth != 0 || args.Count != (pan ? 4 : 3))
                    throw new InvalidOperationException("Unsupported Poiyomi emission sample: " + match.Value);
                int slot = Array.IndexOf(EmissionEncryption.PoiyomiMaps, args[0]);
                string uv = pan ? "POI_PAN_UV(" + args[2] + ", " + args[3] + ")" : args[2];
                string original = code.Substring(match.Index, end - match.Index);
                string replacement = $"(_ShellEmission{slot}Settings.x > 0.5 ? SHELL_EMISSION_SAMPLE({slot}, ({uv}), IsDecrypted()) : {original})";
                code = code.Remove(match.Index, end - match.Index).Insert(match.Index, replacement);
            }
            return code;
        }
    }
}
#endif
