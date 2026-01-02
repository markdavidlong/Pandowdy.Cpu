using System.Reflection;

using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore
{
    public class CharacterRomProvider : ICharacterRomProvider
    {
        private readonly byte[] _characterRom;

        public CharacterRomProvider()
        {
            _characterRom = LoadCharacterRom();
        }

        private static byte[] LoadCharacterRom()
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("Pandowdy.EmuCore.Resources.a2e_enh_video.rom");

            if (stream == null)
            {
                throw new InvalidOperationException("Character ROM resource not found");
            }

            byte[] buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);
            return buffer;
        }

        public ReadOnlySpan<byte> GetGlyph(byte ch, bool flashOn, bool altChar)
        {
            if (!altChar)
            {
                if (ch >= 0x40 && ch < 0x80)
                {
                    ch &= 0x3f;
                    if (!flashOn)
                    {
                        ch |= 0x80;
                    }
                }
            }
            return _characterRom.AsSpan(ch * 8, 8);
        }
    }
}
