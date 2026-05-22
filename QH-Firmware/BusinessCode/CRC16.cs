using System;

namespace QH_Firmware
{
    public static class CRC16
    {
        /// <summary>
        /// 与你的 GD32 硬件完全一致
        /// Poly: 0x1021
        /// Init: 0x0000
        /// 无输入反转
        /// 无输出反转
        /// 无输出异或
        /// </summary>
        public static ushort CalculateCCITT(byte[] data)
        {
            ushort crc = 0x0000;  // 必须是 0x0000
            ushort poly = 0x1021;

            foreach (byte b in data)
            {
                crc ^= (ushort)(b << 8);

                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ poly);
                    else
                        crc <<= 1;
                }
            }

            return crc;
        }
    }
}