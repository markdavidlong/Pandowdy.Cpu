namespace Pandowdy.Tests
{
    using Pandowdy.EmuCore;

    public class TestMemoryPool
    {


        static public MemoryPool BuildPool()
        {
            var pool = new MemoryPool();
            for (int i = 0; i < 65536; i++)
            {
                pool.WritePool(i, 1);
                pool.WritePool(i + 65536, 2);
            }
            byte bank = 0;
            for (int i = 0x20100; i < 0x20800; i+=0x100) // Internal ROM
            {
                bank++;
                for (int j = 0; j < 0x100; j++)
                {
                    //  pool.WritePool(i + j, (byte) (bank+100));
                    pool.WritePool(i + j, (byte) 'I');
                }
            }
            for (int i = 0x24000; i < 0x24700; i += 0x100) // Slot ROM
            {
                bank++;
                for (int j = 0; j < 0x100; j++)
                {
                    // pool.WritePool(i + j, (byte) (bank + 200));
                    pool.WritePool(i + j, (byte) 'S');
                }
            }
            System.Diagnostics.Debug.WriteLine("Pool built");
            return pool;
        }


        [Fact]
        public void Test_ReadPool()
        {
            var pool = BuildPool();

            for (ushort i = 0x200; i < 48 * 1024; i++)
            {
                Assert.Equal(1, pool.ReadPool(i));
            }

            for (ushort i = 0x200; i < 48 * 1024; i++)
            {
                Assert.Equal(2, pool.ReadPool(0x10000 + i));
            }
        }

        [Fact]
        public void Test_Read_MainMemory()
        {
            var pool = BuildPool();

            for (ushort i = 0x200; i < 48 * 1024; i++)
            {
                Assert.Equal(1, pool.Read(i));
            }
        }

        [Fact]
        public void Test_Read_AuxMemory()
        {
            var pool = BuildPool();

            pool.SetRamRd(true);

            for (ushort i = 0x200; i < 48 * 1024; i++)
            {
                Assert.Equal(2, pool.Read(i));
            }
        }



        [Fact]
        public void Test_80StoreOff_RamRdOff()  // 0-3
        {
            var pool = BuildPool();
            pool.Set80Store(false);
            pool.SetRamRd(false);
            pool.SetHiRes(false);
            pool.SetPage2(false);

            // 0x200 = Main (1), 0x400 = Main (1), 0x2000 = Main (1), 0x 4000 = Main (1)
            Assert.Equal(1, pool.Read(0x200));
            Assert.Equal(1, pool.Read(0x400));
            Assert.Equal(1, pool.Read(0x2000));
            Assert.Equal(1, pool.Read(0x4000));

            pool.SetHiRes(false);
            pool.SetPage2(false);

            // 0x200 = Main (1), 0x400 = Main (1), 0x2000 = Main (1), 0x 4000 = Main (1)
            Assert.Equal(1, pool.Read(0x200));
            Assert.Equal(1, pool.Read(0x400));
            Assert.Equal(1, pool.Read(0x2000));
            Assert.Equal(1, pool.Read(0x4000));

            pool.SetHiRes(true);
            pool.SetPage2(false);

            Assert.Equal(1, pool.Read(0x200));
            Assert.Equal(1, pool.Read(0x400));
            Assert.Equal(1, pool.Read(0x2000));
            Assert.Equal(1, pool.Read(0x4000));

            pool.SetHiRes(true);
            pool.SetPage2(true);

            Assert.Equal(1, pool.Read(0x200));
            Assert.Equal(1, pool.Read(0x400));
            Assert.Equal(1, pool.Read(0x2000));
            Assert.Equal(1, pool.Read(0x4000));

            pool.SetHiRes(false);
            pool.SetPage2(true);

            Assert.Equal(1, pool.Read(0x200));
            Assert.Equal(1, pool.Read(0x400));
            Assert.Equal(1, pool.Read(0x2000));
            Assert.Equal(1, pool.Read(0x4000));

        }

        [Fact]
        public void Test_80StoreOff_RamRdOn()  // 4-7
        {
            var pool = BuildPool();
            pool.Set80Store(false);
            pool.SetRamRd(true);

            pool.SetHiRes(false);
            pool.SetPage2(false);

            // 0x200 = Aux (2), 0x400 = Aux (2), 0x2000 = Aux (2), 0x 4000 = Aux (2)
            Assert.Equal(2, pool.Read(0x200));
            Assert.Equal(2, pool.Read(0x400));
            Assert.Equal(2, pool.Read(0x2000));
            Assert.Equal(2, pool.Read(0x4000));

            pool.SetHiRes(false);
            pool.SetPage2(false);

            Assert.Equal(2, pool.Read(0x200));
            Assert.Equal(2, pool.Read(0x400));
            Assert.Equal(2, pool.Read(0x2000));
            Assert.Equal(2, pool.Read(0x4000));

            pool.SetHiRes(true);
            pool.SetPage2(false);

            Assert.Equal(2, pool.Read(0x200));
            Assert.Equal(2, pool.Read(0x400));
            Assert.Equal(2, pool.Read(0x2000));
            Assert.Equal(2, pool.Read(0x4000));

            pool.SetHiRes(true);
            pool.SetPage2(true);

            Assert.Equal(2, pool.Read(0x200));
            Assert.Equal(2, pool.Read(0x400));
            Assert.Equal(2, pool.Read(0x2000));
            Assert.Equal(2, pool.Read(0x4000));

            pool.SetHiRes(false);
            pool.SetPage2(true);

            Assert.Equal(2, pool.Read(0x200));
            Assert.Equal(2, pool.Read(0x400));
            Assert.Equal(2, pool.Read(0x2000));
            Assert.Equal(2, pool.Read(0x4000));
        }


        [Fact]
        public void Test_80StoreOn_RamRdOff_HiResOff_Page2Off()  // 8
        {
            var pool = BuildPool();
            pool.Set80Store(true);
            pool.SetRamRd(false);
            pool.SetHiRes(false);
            pool.SetPage2(false);
            // 0x200 = Main (1), 0x400 = Main (1), 0x2000 = Main (1), 0x 4000 = Main (1)
            Assert.Equal(1, pool.Read(0x200));
            Assert.Equal(1, pool.Read(0x400));
            Assert.Equal(1, pool.Read(0x2000));
            Assert.Equal(1, pool.Read(0x4000));
        }

        [Fact]
        public void Test_80StoreOn_RamRdOn_HiResOff_Page2On()  // 13
        {
            var pool = BuildPool();
            pool.Set80Store(true);
            pool.SetRamRd(true);
            pool.SetHiRes(false);
            pool.SetPage2(true);
            // 0x200 = Aux (2), 0x400 = Aux (2), 0x2000 = Aux (2), 0x 4000 = Aux (2)
            Assert.Equal(2, pool.Read(0x200));
            Assert.Equal(2, pool.Read(0x400));
            Assert.Equal(2, pool.Read(0x2000));
            Assert.Equal(2, pool.Read(0x4000));
        }


        [Fact]
        public void Test_80StoreOn_RamRdOff_HiResOn_Page2Off()  // 10
        {
            var pool = BuildPool();
            pool.Set80Store(true);
            pool.SetRamRd(false);
            pool.SetHiRes(true);
            pool.SetPage2(false);

            // 0x200 = Main (1), 0x400 = Main (1), 0x2000 = Main (1), 0x 4000 = Main (1)
            Assert.Equal(1, pool.Read(0x200));
            Assert.Equal(1, pool.Read(0x400));
            Assert.Equal(1, pool.Read(0x2000));
            Assert.Equal(1, pool.Read(0x4000));
        }

        [Fact]
        public void Test_80StoreOn_RamRdOn_HiResOn_Page2On()  // 15
        {
            var pool = BuildPool();
            pool.Set80Store(true);
            pool.SetRamRd(true);
            pool.SetHiRes(true);
            pool.SetPage2(true);
            // 0x200 = Aux (2), 0x400 = Aux (2), 0x2000 = Aux (2), 0x 4000 = Aux (2)
            Assert.Equal(2, pool.Read(0x200));
            Assert.Equal(2, pool.Read(0x400));
            Assert.Equal(2, pool.Read(0x2000));
            Assert.Equal(2, pool.Read(0x4000));
        }

        [Fact]
        public void Test_80StoreOn_RamRdOff_HiResOff_Page2On()  // 9
        {
            var pool = BuildPool();
            pool.Set80Store(true);
            pool.SetRamRd(false);
            pool.SetHiRes(false);
            pool.SetPage2(true);
            // 0x200 = Main (1), 0x400 = Aux (2), 0x2000 = Main (1), 0x 4000 = Main (1)
            Assert.Equal(1, pool.Read(0x200));
            Assert.Equal(2, pool.Read(0x400));
            Assert.Equal(1, pool.Read(0x2000));
            Assert.Equal(1, pool.Read(0x4000));
        }
        [Fact]
        public void Test_80StoreOn_RamRdOff_HiResOn_Page2On()  // 11
        {
            var pool = BuildPool();
            pool.Set80Store(true);
            pool.SetRamRd(false);
            pool.SetHiRes(true);
            pool.SetPage2(true);
            // 0x200 = Main (1), 0x400 = Aux (2), 0x2000 = Aux (2), 0x 4000 = Main (1)
            Assert.Equal(1, pool.Read(0x200));
            Assert.Equal(2, pool.Read(0x400));
            Assert.Equal(2, pool.Read(0x2000));
            Assert.Equal(1, pool.Read(0x4000));
        }

        // Test On On Off Off, which should be Aux, Main, Aux, Aux
        [Fact]
        public void Test_80StoreOn_RamRdOn_HiResOff_Page2Off()  // 12
        {
            var pool = BuildPool();
            pool.Set80Store(true);
            pool.SetRamRd(true);
            pool.SetHiRes(false);
            pool.SetPage2(false);
            // 0x200 = Aux (2), 0x400 = Main (1), 0x2000 = Aux (2), 0x 4000 = Aux (2)
            Assert.Equal(2, pool.Read(0x200));
            Assert.Equal(1, pool.Read(0x400));
            Assert.Equal(2, pool.Read(0x2000));
            Assert.Equal(2, pool.Read(0x4000));
        }

        // Test On On On off which should be Aux , Main, Main, Aux
        [Fact]
        public void Test_80StoreOn_RamRdOn_HiResOn_Page2Off()  // 14
        {
            var pool = BuildPool();
            pool.Set80Store(true);
            pool.SetRamRd(true);
            pool.SetHiRes(true);
            pool.SetPage2(false);
            // 0x200 = Aux (2), 0x400 = Main (1), 0x2000 = Main (1), 0x 4000 = Aux (2)
            Assert.Equal(2, pool.Read(0x200));
            Assert.Equal(1, pool.Read(0x400));
            Assert.Equal(1, pool.Read(0x2000));
            Assert.Equal(2, pool.Read(0x4000));
        }

        /////////////////////////////////////
        ///


        [Fact]
        public void Test_80StoreOff_RamWrtOff()  // 0-3
        {
            var pool = BuildPool();
            pool.Set80Store(false);
            pool.SetRamWrt(false);


            pool.SetHiRes(false);
            pool.SetPage2(false);

            // 0x200 = Main (1), 0x400 = Main (1), 0x2000 = Main (1), 0x 4000 = Main (1)
            pool.SetHiRes(false);
            pool.SetPage2(false);

            pool.Write(0x200, 3);
            pool.Write(0x400, 3);
            pool.Write(0x2000, 3);
            pool.Write(0x4000, 3);

            Assert.Equal(3, pool.ReadRawMain(0x200));
            Assert.Equal(3, pool.ReadRawMain(0x400));
            Assert.Equal(3, pool.ReadRawMain(0x2000));
            Assert.Equal(3, pool.ReadRawMain(0x4000));
            Assert.Equal(2, pool.ReadRawAux(0x200));
            Assert.Equal(2, pool.ReadRawAux(0x400));
            Assert.Equal(2, pool.ReadRawAux(0x2000));
            Assert.Equal(2, pool.ReadRawAux(0x4000));


            pool.SetHiRes(true);
            pool.SetPage2(false);

            pool.Write(0x200, 4);
            pool.Write(0x400, 4);
            pool.Write(0x2000, 4);
            pool.Write(0x4000, 4);

            Assert.Equal(4, pool.ReadRawMain(0x200));
            Assert.Equal(4, pool.ReadRawMain(0x400));
            Assert.Equal(4, pool.ReadRawMain(0x2000));
            Assert.Equal(4, pool.ReadRawMain(0x4000));
            Assert.Equal(2, pool.ReadRawAux(0x200));
            Assert.Equal(2, pool.ReadRawAux(0x400));
            Assert.Equal(2, pool.ReadRawAux(0x2000));
            Assert.Equal(2, pool.ReadRawAux(0x4000));



            pool.SetHiRes(true);
            pool.SetPage2(true);

            pool.Write(0x200, 53);
            pool.Write(0x400, 53);
            pool.Write(0x2000, 53);
            pool.Write(0x4000, 53);

            Assert.Equal(53, pool.ReadRawMain(0x200));
            Assert.Equal(53, pool.ReadRawMain(0x400));
            Assert.Equal(53, pool.ReadRawMain(0x2000));
            Assert.Equal(53, pool.ReadRawMain(0x4000));
            Assert.Equal(2, pool.ReadRawAux(0x200));
            Assert.Equal(2, pool.ReadRawAux(0x400));
            Assert.Equal(2, pool.ReadRawAux(0x2000));
            Assert.Equal(2, pool.ReadRawAux(0x4000));

            pool.SetHiRes(false);
            pool.SetPage2(true);


            pool.Write(0x200, 23);
            pool.Write(0x400, 23);
            pool.Write(0x2000, 23);
            pool.Write(0x4000, 23);

            Assert.Equal(23, pool.ReadRawMain(0x200));
            Assert.Equal(23, pool.ReadRawMain(0x400));
            Assert.Equal(23, pool.ReadRawMain(0x2000));
            Assert.Equal(23, pool.ReadRawMain(0x4000));
            Assert.Equal(2, pool.ReadRawAux(0x200));
            Assert.Equal(2, pool.ReadRawAux(0x400));
            Assert.Equal(2, pool.ReadRawAux(0x2000));
            Assert.Equal(2, pool.ReadRawAux(0x4000));


        }

        [Fact]
        public void Test_80StoreOff_RamWrtOn()  // 4-7
        {
            var pool = BuildPool();
            pool.Set80Store(false);
            pool.SetRamWrt(true);

            // 0x200 = Aux (2), 0x400 = Aux (2), 0x2000 = Aux (2), 0x 4000 = Aux (2)
            pool.SetHiRes(false);
            pool.SetPage2(false);

            pool.Write(0x200, 3);
            pool.Write(0x400, 3);
            pool.Write(0x2000, 3);
            pool.Write(0x4000, 3);

            Assert.Equal(1, pool.ReadRawMain(0x200));
            Assert.Equal(1, pool.ReadRawMain(0x400));
            Assert.Equal(1, pool.ReadRawMain(0x2000));
            Assert.Equal(1, pool.ReadRawMain(0x4000));
            Assert.Equal(3, pool.ReadRawAux(0x200));
            Assert.Equal(3, pool.ReadRawAux(0x400));
            Assert.Equal(3, pool.ReadRawAux(0x2000));
            Assert.Equal(3, pool.ReadRawAux(0x4000));

            pool.SetHiRes(false);
            pool.SetPage2(false);

            pool.Write(0x200, 23);
            pool.Write(0x400, 23);
            pool.Write(0x2000, 23);
            pool.Write(0x4000, 23);
            Assert.Equal(1, pool.ReadRawMain(0x200));
            Assert.Equal(1, pool.ReadRawMain(0x400));
            Assert.Equal(1, pool.ReadRawMain(0x2000));
            Assert.Equal(1, pool.ReadRawMain(0x4000));
            Assert.Equal(23, pool.ReadRawAux(0x200));
            Assert.Equal(23, pool.ReadRawAux(0x400));
            Assert.Equal(23, pool.ReadRawAux(0x2000));
            Assert.Equal(23, pool.ReadRawAux(0x4000));

            pool.SetHiRes(true);
            pool.SetPage2(false);

            pool.Write(0x200, 33);
            pool.Write(0x400, 33);
            pool.Write(0x2000, 33);
            pool.Write(0x4000, 33);
            Assert.Equal(1, pool.ReadRawMain(0x200));
            Assert.Equal(1, pool.ReadRawMain(0x400));
            Assert.Equal(1, pool.ReadRawMain(0x2000));
            Assert.Equal(1, pool.ReadRawMain(0x4000));
            Assert.Equal(33, pool.ReadRawAux(0x200));
            Assert.Equal(33, pool.ReadRawAux(0x400));
            Assert.Equal(33, pool.ReadRawAux(0x2000));
            Assert.Equal(33, pool.ReadRawAux(0x4000));

            pool.SetHiRes(true);
            pool.SetPage2(true);

            pool.Write(0x200, 13);
            pool.Write(0x400, 13);
            pool.Write(0x2000, 13);
            pool.Write(0x4000, 13);
            Assert.Equal(1, pool.ReadRawMain(0x200));
            Assert.Equal(1, pool.ReadRawMain(0x400));
            Assert.Equal(1, pool.ReadRawMain(0x2000));
            Assert.Equal(1, pool.ReadRawMain(0x4000));
            Assert.Equal(13, pool.ReadRawAux(0x200));
            Assert.Equal(13, pool.ReadRawAux(0x400));
            Assert.Equal(13, pool.ReadRawAux(0x2000));
            Assert.Equal(13, pool.ReadRawAux(0x4000));

            pool.SetHiRes(false);
            pool.SetPage2(true);

            pool.Write(0x200, 3);
            pool.Write(0x400, 3);
            pool.Write(0x2000, 3);
            pool.Write(0x4000, 3);

            Assert.Equal(1, pool.ReadRawMain(0x200));
            Assert.Equal(1, pool.ReadRawMain(0x400));
            Assert.Equal(1, pool.ReadRawMain(0x2000));
            Assert.Equal(1, pool.ReadRawMain(0x4000));
            Assert.Equal(3, pool.ReadRawAux(0x200));
            Assert.Equal(3, pool.ReadRawAux(0x400));
            Assert.Equal(3, pool.ReadRawAux(0x2000));
            Assert.Equal(3, pool.ReadRawAux(0x4000));
        }


        [Fact]
        public void Test_80StoreOn_RamWrtOff_HiResOff_Page2Off()  // 8
        {
            var pool = BuildPool();
            pool.Set80Store(true);
            pool.SetRamWrt(false);
            pool.SetHiRes(false);
            pool.SetPage2(false);
            // 0x200 = Main (1), 0x400 = Main (1), 0x2000 = Main (1), 0x 4000 = Main (1)
            pool.Write(0x200, 23);
            pool.Write(0x400, 23);
            pool.Write(0x2000, 23);
            pool.Write(0x4000, 23);

            Assert.Equal(23, pool.ReadRawMain(0x200));
            Assert.Equal(23, pool.ReadRawMain(0x400));
            Assert.Equal(23, pool.ReadRawMain(0x2000));
            Assert.Equal(23, pool.ReadRawMain(0x4000));
            Assert.Equal(2, pool.ReadRawAux(0x200));
            Assert.Equal(2, pool.ReadRawAux(0x400));
            Assert.Equal(2, pool.ReadRawAux(0x2000));
            Assert.Equal(2, pool.ReadRawAux(0x4000));

        }

        [Fact]
        public void Test_80StoreOn_RamWrtOn_HiResOff_Page2On()  // 13
        {
            var pool = BuildPool();
            pool.Set80Store(true);
            pool.SetRamWrt(true);
            pool.SetHiRes(false);
            pool.SetPage2(true);
            // 0x200 = Aux (2), 0x400 = Aux (2), 0x2000 = Aux (2), 0x 4000 = Aux (2)
            pool.Write(0x200, 33);
            pool.Write(0x400, 33);
            pool.Write(0x2000, 33);
            pool.Write(0x4000, 33);
            Assert.Equal(1, pool.ReadRawMain(0x200));
            Assert.Equal(1, pool.ReadRawMain(0x400));
            Assert.Equal(1, pool.ReadRawMain(0x2000));
            Assert.Equal(1, pool.ReadRawMain(0x4000));
            Assert.Equal(33, pool.ReadRawAux(0x200));
            Assert.Equal(33, pool.ReadRawAux(0x400));
            Assert.Equal(33, pool.ReadRawAux(0x2000));
            Assert.Equal(33, pool.ReadRawAux(0x4000));

        }


        [Fact]
        public void Test_80StoreOn_RamWrtOff_HiResOn_Page2Off()  // 10
        {
            var pool = BuildPool();
            pool.Set80Store(true);
            pool.SetRamWrt(false);
            pool.SetHiRes(true);
            pool.SetPage2(false);

            // 0x200 = Main (1), 0x400 = Main (1), 0x2000 = Main (1), 0x 4000 = Main (1)
            pool.Write(0x200, 23);
            pool.Write(0x400, 23);
            pool.Write(0x2000, 23);
            pool.Write(0x4000, 23);

            Assert.Equal(23, pool.ReadRawMain(0x200));
            Assert.Equal(23, pool.ReadRawMain(0x400));
            Assert.Equal(23, pool.ReadRawMain(0x2000));
            Assert.Equal(23, pool.ReadRawMain(0x4000));
            Assert.Equal(2, pool.ReadRawAux(0x200));
            Assert.Equal(2, pool.ReadRawAux(0x400));
            Assert.Equal(2, pool.ReadRawAux(0x2000));
            Assert.Equal(2, pool.ReadRawAux(0x4000));
        }

        [Fact]
        public void Test_80StoreOn_RamWrtOn_HiResOn_Page2On()  // 15
        {
            var pool = BuildPool();
            pool.Set80Store(true);
            pool.SetRamWrt(true);
            pool.SetHiRes(true);
            pool.SetPage2(true);
            // 0x200 = Aux (2), 0x400 = Aux (2), 0x2000 = Aux (2), 0x 4000 = Aux (2)
            pool.Write(0x200, 33);
            pool.Write(0x400, 33);
            pool.Write(0x2000, 33);
            pool.Write(0x4000, 33);
            Assert.Equal(1, pool.ReadRawMain(0x200));
            Assert.Equal(1, pool.ReadRawMain(0x400));
            Assert.Equal(1, pool.ReadRawMain(0x2000));
            Assert.Equal(1, pool.ReadRawMain(0x4000));
            Assert.Equal(33, pool.ReadRawAux(0x200));
            Assert.Equal(33, pool.ReadRawAux(0x400));
            Assert.Equal(33, pool.ReadRawAux(0x2000));
            Assert.Equal(33, pool.ReadRawAux(0x4000));

        }

        [Fact]
        public void Test_80StoreOn_RamWrtOff_HiResOff_Page2On()  // 9
        {
            var pool = BuildPool();
            pool.Set80Store(true);
            pool.SetRamWrt(false);
            pool.SetHiRes(false);
            pool.SetPage2(true);
            // 0x200 = Main (1), 0x400 = Aux (2), 0x2000 = Main (1), 0x 4000 = Main (1)
            pool.Write(0x200, 33);
            pool.Write(0x400, 33);
            pool.Write(0x2000, 33);
            pool.Write(0x4000, 33);
            Assert.Equal(33, pool.ReadRawMain(0x200));
            Assert.Equal(1, pool.ReadRawMain(0x400));
            Assert.Equal(33, pool.ReadRawMain(0x2000));
            Assert.Equal(33, pool.ReadRawMain(0x4000));

            Assert.Equal(2, pool.ReadRawAux(0x200));
            Assert.Equal(33, pool.ReadRawAux(0x400));
            Assert.Equal(2, pool.ReadRawAux(0x2000));
            Assert.Equal(2, pool.ReadRawAux(0x4000));

        }
        [Fact]
        public void Test_80StoreOn_RamWrtOff_HiResOn_Page2On()  // 11
        {
            var pool = BuildPool();
            pool.Set80Store(true);
            pool.SetRamWrt(false);
            pool.SetHiRes(true);
            pool.SetPage2(true);
            // 0x200 = Main (1), 0x400 = Aux (2), 0x2000 = Aux (2), 0x 4000 = Main (1)
            pool.Write(0x200, 33);
            pool.Write(0x400, 33);
            pool.Write(0x2000, 33);
            pool.Write(0x4000, 33);
            Assert.Equal(33, pool.ReadRawMain(0x200));
            Assert.Equal(1, pool.ReadRawMain(0x400));
            Assert.Equal(1, pool.ReadRawMain(0x2000));
            Assert.Equal(33, pool.ReadRawMain(0x4000));

            Assert.Equal(2, pool.ReadRawAux(0x200));
            Assert.Equal(33, pool.ReadRawAux(0x400));
            Assert.Equal(33, pool.ReadRawAux(0x2000));
            Assert.Equal(2, pool.ReadRawAux(0x4000));

        }

        // Test On On Off Off, which should be Aux, Main, Aux, Aux
        [Fact]
        public void Test_80StoreOn_RamWrtOn_HiResOff_Page2Off()  // 12
        {
            var pool = BuildPool();
            pool.Set80Store(true);
            pool.SetRamWrt(true);
            pool.SetHiRes(false);
            pool.SetPage2(false);
            // 0x200 = Aux (2), 0x400 = Main (1), 0x2000 = Aux (2), 0x 4000 = Aux (2)

            pool.Write(0x200, 33);
            pool.Write(0x400, 33);
            pool.Write(0x2000, 33);
            pool.Write(0x4000, 33);
            Assert.Equal(1, pool.ReadRawMain(0x200));
            Assert.Equal(33, pool.ReadRawMain(0x400));
            Assert.Equal(1, pool.ReadRawMain(0x2000));
            Assert.Equal(1, pool.ReadRawMain(0x4000));

            Assert.Equal(33, pool.ReadRawAux(0x200));
            Assert.Equal(2, pool.ReadRawAux(0x400));
            Assert.Equal(33, pool.ReadRawAux(0x2000));
            Assert.Equal(33, pool.ReadRawAux(0x4000));

        }

        // Test On On On off which should be Aux , Main, Main, Aux
        [Fact]
        public void Test_80StoreOn_RamWrtOn_HiResOn_Page2Off()  // 14
        {
            var pool = BuildPool();
            pool.Set80Store(true);
            pool.SetRamWrt(true);
            pool.SetHiRes(true);
            pool.SetPage2(false);
            // 0x200 = Aux (2), 0x400 = Main (1), 0x2000 = Main (1), 0x 4000 = Aux (2)
            pool.Write(0x200, 33);
            pool.Write(0x400, 33);
            pool.Write(0x2000, 33);
            pool.Write(0x4000, 33);
            Assert.Equal(1, pool.ReadRawMain(0x200));
            Assert.Equal(33, pool.ReadRawMain(0x400));
            Assert.Equal(33, pool.ReadRawMain(0x2000));
            Assert.Equal(1, pool.ReadRawMain(0x4000));

            Assert.Equal(33, pool.ReadRawAux(0x200));
            Assert.Equal(2, pool.ReadRawAux(0x400));
            Assert.Equal(2, pool.ReadRawAux(0x2000));
            Assert.Equal(33, pool.ReadRawAux(0x4000));
        }

        [Fact]
        public void TestIntCxOn_SlotC3Off()
        {
            var pool = BuildPool();
            pool.SetIntCxRom(true);
            pool.SetSlotC3Rom(false);

            Assert.Equal((byte) 'I', pool.Read(0xC100));
            Assert.Equal((byte) 'I', pool.Read(0xC200));
            Assert.Equal((byte) 'I', pool.Read(0xC300));
            Assert.Equal((byte) 'I', pool.Read(0xC400));
            Assert.Equal((byte) 'I', pool.Read(0xC500));
            Assert.Equal((byte) 'I', pool.Read(0xC600));
            Assert.Equal((byte) 'I', pool.Read(0xC700));
        }

        [Fact]
        public void TestIntCxOn_SlotC3On()
        {
            var pool = BuildPool();
            pool.SetIntCxRom(true);
            pool.SetSlotC3Rom(true);
            Assert.Equal((byte) 'I', pool.Read(0xC100));
            Assert.Equal((byte) 'I', pool.Read(0xC200));
            Assert.Equal((byte) 'I', pool.Read(0xC300));
            Assert.Equal((byte) 'I', pool.Read(0xC400));
            Assert.Equal((byte) 'I', pool.Read(0xC500));
            Assert.Equal((byte) 'I', pool.Read(0xC600));
            Assert.Equal((byte) 'I', pool.Read(0xC700));
        }

        [Fact]
        public void TestIntCxOff_SlotC3Off()
        {
            var pool = BuildPool();
            pool.SetIntCxRom(false);
            pool.SetSlotC3Rom(false);
            Assert.Equal((byte) 'S', pool.Read(0xC100));
            Assert.Equal((byte) 'S', pool.Read(0xC200));
            Assert.Equal((byte) 'I', pool.Read(0xC300));
            Assert.Equal((byte) 'S', pool.Read(0xC400));
            Assert.Equal((byte) 'S', pool.Read(0xC500));
            Assert.Equal((byte) 'S', pool.Read(0xC600));
            Assert.Equal((byte) 'S', pool.Read(0xC700));
        }

        [Fact]
        public void TestIntCxOff_SlotC3On()
        {
            var pool = BuildPool();
            pool.SetIntCxRom(false);
            pool.SetSlotC3Rom(true);
            Assert.Equal((byte) 'S', pool.Read(0xC100));
            Assert.Equal((byte) 'S', pool.Read(0xC200));
            Assert.Equal((byte) 'S', pool.Read(0xC300));
            Assert.Equal((byte) 'S', pool.Read(0xC400));
            Assert.Equal((byte) 'S', pool.Read(0xC500));
            Assert.Equal((byte) 'S', pool.Read(0xC600));
            Assert.Equal((byte) 'S', pool.Read(0xC700));
        }



    }
}