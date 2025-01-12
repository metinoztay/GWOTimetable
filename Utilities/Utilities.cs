
using System.Security.Cryptography;
using System.Text;
public static class Utilities
{
    public static string CreateHash(string password)
    {

        MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
        byte[] tempString = Encoding.UTF8.GetBytes(password);
        tempString = md5.ComputeHash(tempString);
        StringBuilder sb = new StringBuilder();

        foreach (byte ba in tempString)
        {
            sb.Append(ba.ToString("x2").ToLower());
        }

        return sb.ToString();
    }

    public static string GeneratePassword(int length = 12)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
                             "abcdefghijklmnopqrstuvwxyz" +
                             "0123456789" +
                             "!@#$%&*()_+=.?";

        Random rand = new Random();

        char[] passwordChars = new char[length];

        for (int i = 0; i < length; i++)
        {
            passwordChars[i] = chars[rand.Next(chars.Length)];
        }

        return new string(passwordChars);
    }

    public static string ToProperCase(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        str = System.Text.RegularExpressions.Regex.Replace(str, @"\s+", " ");
        return string.Concat(str.Select((x, i) => i == 0 || char.IsWhiteSpace(str[i - 1]) ? char.ToUpper(x) : char.ToLower(x)));
    }


    public static IEnumerable<string> GetVariations(int number)
    {
        if (number == 0)
            yield return string.Empty;

        for (int i = 1; i <= number; i++)
        {
            foreach (var variation in GetVariations(number - i))
            {
                yield return i + (string.IsNullOrEmpty(variation) ? string.Empty : "," + variation);
            }
        }
    }
}
