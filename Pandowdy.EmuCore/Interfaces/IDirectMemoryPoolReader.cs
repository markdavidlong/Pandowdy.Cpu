namespace Pandowdy.EmuCore.Interfaces
{
    public interface IDirectMemoryPoolReader
    {

        /// <summary>
        /// Read a byte from the main memory pool, bypassing bus mapping. 
        /// </summary>
        /// <param name="address">Address to read from
        /// 
        /// Note: D000-DFFF page 1 is mapped to C000-CFFF, page 2 is actually at D000-DFFF
        /// </param>
        /// <returns>Value at main memory pool address or 0 if not available.</returns>
        byte ReadRawMain(int address);

        /// <summary>
        /// Read a byte from the aux memory pool, bypassing bus mapping. 
        /// </summary>
        /// <param name="address">Address to read from
        /// 
        /// Note: D000-DFFF page 1 is mapped to C000-CFFF, page 2 is actually at D000-DFFF
        /// </param>
        /// <returns>Value at aux memory pool address or 0 if not available.</returns>
        byte ReadRawAux(int address);
    }
}

