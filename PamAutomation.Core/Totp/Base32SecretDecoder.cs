namespace PamAutomation.Core.Totp;

internal static class Base32SecretDecoder
{
    private const int BitsPerSymbol = 5;
    private const int BitsPerByte = 8;

    public static byte[] Decode(string encodedSecret)
    {
        ArgumentNullException.ThrowIfNull(encodedSecret);

        int symbolCount = 0;
        int paddingCount = 0;
        bool paddingStarted = false;

        foreach (char character in encodedSecret)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            if (character == '=')
            {
                paddingStarted = true;
                paddingCount++;
                continue;
            }

            if (paddingStarted || GetSymbolValue(character) < 0)
            {
                throw new InvalidBase32SecretException();
            }

            symbolCount++;
        }

        ValidateLength(symbolCount, paddingCount);

        var decoded = new byte[symbolCount * BitsPerSymbol / BitsPerByte];
        int buffer = 0;
        int bufferedBits = 0;
        int outputIndex = 0;

        foreach (char character in encodedSecret)
        {
            if (char.IsWhiteSpace(character) || character == '=')
            {
                continue;
            }

            buffer = (buffer << BitsPerSymbol) | GetSymbolValue(character);
            bufferedBits += BitsPerSymbol;

            if (bufferedBits < BitsPerByte)
            {
                continue;
            }

            bufferedBits -= BitsPerByte;
            decoded[outputIndex++] = (byte)(buffer >> bufferedBits);
            buffer &= (1 << bufferedBits) - 1;
        }

        if (buffer != 0)
        {
            Array.Clear(decoded);
            throw new InvalidBase32SecretException();
        }

        return decoded;
    }

    private static void ValidateLength(int symbolCount, int paddingCount)
    {
        if (symbolCount == 0)
        {
            throw new InvalidBase32SecretException();
        }

        int symbolRemainder = symbolCount % 8;
        int expectedPadding = symbolRemainder switch
        {
            0 => 0,
            2 => 6,
            4 => 4,
            5 => 3,
            7 => 1,
            _ => -1
        };

        if (expectedPadding < 0 || (paddingCount > 0 && paddingCount != expectedPadding))
        {
            throw new InvalidBase32SecretException();
        }
    }

    private static int GetSymbolValue(char character)
    {
        if (character is >= 'A' and <= 'Z')
        {
            return character - 'A';
        }

        if (character is >= 'a' and <= 'z')
        {
            return character - 'a';
        }

        if (character is >= '2' and <= '7')
        {
            return character - '2' + 26;
        }

        return -1;
    }
}
