using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASYST.ver2
{
    class EVBattery : System.IComparable, System.IComparable<EVBattery>
    {
        private int carID;
        private DateTime arriveTime = new DateTime();
        private DateTime departureTime = new DateTime();
        private double[] ChargeCapacity = new double[24];   //(正の数想定)
        private double[] DischargeCapacity = new double[24];    //(負の数想定)
        private double homeEnergy;
        private double freeBattery = 12;//8割充電3割残し
        private double chargeSpeedUpper = 8.55;//DC 19A, 450V
        private double dischargeSpeedUpper = 6.0;//AC 30A, 100V *2

       
        public object Clone()    //オブジェクトのコピー
        {
            return MemberwiseClone();
        }
        //コンストラクタ
        /// <summary>
        /// EVのバッテリー
        /// </summary>
        /// <param name="carID">車ID</param>
        /// <param name="arrive">到着時刻</param>
        /// <param name="departure">出発時刻</param>
        /// <param name="OutEnergy">来るときに使った電力量</param>
        /// <param name="homeEnergy">出発時に必要な電力量</param>
        public EVBattery(int carID, DateTime arrive, DateTime departure, double OutEnergy, double homeEnergy)
        {
            this.carID = carID;
            arriveTime = arrive;
            departureTime = departure;

            if(arriveTime.Date != departureTime.Date)
            {
                departureTime = new DateTime(departureTime.Year, departureTime.Month, departureTime.Day, 23, 59, 59);
            }

            for (int i = 0; i < 24; i++)
            {
                if (i < arriveTime.Hour || departureTime.Hour <= i)
                {
                    ChargeCapacity[i] = 0;
                    DischargeCapacity[i] = 0;
                }
                else if (arriveTime.Hour <= i && i < departureTime.Hour)
                {
                    ChargeCapacity[i] = OutEnergy;
                    DischargeCapacity[i] = -(freeBattery - OutEnergy - homeEnergy);
                }
            }
            this.homeEnergy = homeEnergy;
        }
        public double getChargeCapacity(int time)
        {
            return ChargeCapacity[time];
        }
        public double getDischargeCapacity(int time)
        {
            return DischargeCapacity[time];
        }

        public override string ToString()
        {
            string str = "EVBattery CarID:" + carID + "  \r\n";
            str += "Time, ChargeCapacity, DischargeCapacity \r\n";
            for (int i = 0; i < 24; i++) { str += i + ":00, " + ChargeCapacity[i] + ", " + DischargeCapacity[i] + "\r\n"; }
            return str;
        }


        public double Charge(int time, double Energy)//引数は負の数、返り値は充電できなかった量(負の数)で充電できなければ引数がそのまま戻される
        {
            double retEnergy = Energy;
            bool retTrue = true;
            for (int i = time; i < 24; i++)
            {
                if (arriveTime.Hour <= i && i < departureTime.Hour)
                {
                    //求充電量、充電速度、キャパの一番小さいものによって充電量が変わる
                    if (ChargeCapacity[i] >= Math.Abs(Energy) && Math.Abs(Energy) <= chargeSpeedUpper)//求充電量
                    {
                        if (retTrue)
                        {
                            retEnergy = 0;
                        }
                        ChargeCapacity[i] += Energy;
                        DischargeCapacity[i] += Energy;
                    }
                    //最大まで充電
                    else if (Math.Abs(Energy) >= ChargeCapacity[i] && ChargeCapacity[i] <= chargeSpeedUpper)//キャパ最小
                    {
                        if (retTrue)
                        {
                            retEnergy = ChargeCapacity[i] + Energy;
                        }
                        ChargeCapacity[i] = 0;
                        DischargeCapacity[i] = -freeBattery + homeEnergy;
                    }
                    else if (chargeSpeedUpper <= ChargeCapacity[i] && chargeSpeedUpper < Math.Abs(Energy))//速度最小
                    {
                        if (retTrue)
                        {
                            retEnergy += chargeSpeedUpper;
                        }
                        ChargeCapacity[i] -= chargeSpeedUpper;
                        DischargeCapacity[i] -= chargeSpeedUpper;
                    }
                    retTrue = false;
                }
                else
                {
                    break;
                }
            }
            return retEnergy;
        }

        public double Discharge(int time, double Energy)//引数は正の数、返り値は給電できなかった量(正の数)
        {
            double retEnergy = Energy;
            bool retTrue = true;
            for (int i = time; i < 24; i++)
            {
                if (arriveTime.Hour <= i && i < departureTime.Hour)
                {
                    //求給電量、給電速度、キャパの一番小さいものによって給電量が変わる
                    if (Math.Abs(DischargeCapacity[i]) >= Energy && Energy <= dischargeSpeedUpper) //求給電量最小
                    {
                        if (retTrue)
                        {
                            retEnergy = 0;
                        }
                        ChargeCapacity[i] += Energy;
                        DischargeCapacity[i] += Energy;
                    }
                    else if (Math.Abs(DischargeCapacity[i]) <= Energy && Math.Abs(DischargeCapacity[i]) <= dischargeSpeedUpper)//キャパ最小
                    {
                        if (retTrue)
                        {
                            retEnergy = Energy + DischargeCapacity[i];
                        }
                        ChargeCapacity[i] = freeBattery - homeEnergy;
                        DischargeCapacity[i] = 0;
                    }
                    else if (Math.Abs(DischargeCapacity[i]) >= dischargeSpeedUpper && Energy >= dischargeSpeedUpper)//速度最小
                    {
                        if (retTrue)
                        {
                            retEnergy -= dischargeSpeedUpper;
                        }
                        ChargeCapacity[i] += dischargeSpeedUpper;
                        DischargeCapacity[i] += dischargeSpeedUpper;
                    }

                    retTrue = false;
                }
                else
                {
                    break;
                }
            }
            return retEnergy;
        }

        //出発時間ソート用

        public int CompareTo(EVBattery other)
        {
            //nullより大きい
            if (other == null)
            {
                return 1;
            }

            //Priceを比較する
            return this.departureTime.CompareTo(other.departureTime);
        }

        public int CompareTo(object obj)
        {
            //nullより大きい
            if (obj == null)
            {
                return 1;
            }

            //違う型とは比較できない
            if (this.GetType() != obj.GetType())
            {
                throw new ArgumentException("別の型とは比較できません。", "obj");
            }

            return this.departureTime.CompareTo(((EVBattery)obj).departureTime);
        }
    }
}
