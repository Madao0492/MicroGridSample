using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroGridSample
{
    class MicroGridBattery : ICloneable
    {
        private List<EVBattery> evList = new List<EVBattery>();
        private List<StorageBattery> storageList = new List<StorageBattery>(); //バッテリーは可変容量のもの1つ想定だが念のためリストに
        public object Clone()
        {
            MicroGridBattery mgb = new MicroGridBattery();
            for (int i = 0; i < evList.Count; i++)
            {
                mgb.AddEV((EVBattery)evList[i].Clone());
            }
            for(int j = 0; j < storageList.Count; j++)
            {
                mgb.AddStorage((StorageBattery)storageList[j].Clone());
            }
            return mgb;
        }

        public void AddEV(EVBattery ev)
        {
            evList.Add(ev);
        }

        public void DelEV(int i)
        {
            evList.RemoveAt(i);
        }

        public List<EVBattery> GetEvList()
        {
            return evList;
        }

        public void SetEvList(List<EVBattery> list)
        {
            evList.Clear();
            evList = new List<EVBattery>(list);
        }

        public void AddStorage(StorageBattery Storage)
        {
            storageList.Add(Storage);
        }

        public void DelStorage(int i)
        {
            storageList.RemoveAt(i);
        }

        public List<StorageBattery> GetStorageList()
        {
            return storageList;
        }

        public void SetStorageList(List<StorageBattery> list)
        {
            storageList.Clear();
            storageList = new List<StorageBattery>(list);
        }

        public override string ToString()
        {
            string str = "MicroGridBattery \r\n";
            str += "Time, ChargeCapacity, DischargeCapacity \r\n";
            for (int i = 0; i < 24; i++) { str += i + ":00, " + GetAllChargeCapacity(i) + ", " + GetAllDischargeCapacity(i) + "\r\n"; }
            return str;
        }

        //充電キャパシティの取得
        public double GetAllChargeCapacity(int time)
        {
            double chargeCapacity = 0;
            for (int i = 0; i < evList.Count; i++)
            {
                chargeCapacity += evList[i].getChargeCapacity(time);
            }
            for(int j= 0; j< storageList.Count; j++)
            {
                chargeCapacity += storageList[j].getChargeCapacity(time);
            }
            return chargeCapacity;
        }

        //給電ポテンシャルの取得
        public double GetAllDischargeCapacity(int time)
        {
            double dischargeCapacity = 0;
            for (int i = 0; i < evList.Count; i++)
            {
                dischargeCapacity += evList[i].getDischargeCapacity(time);
            }
            for (int j = 0; j < storageList.Count; j++)
            {
                dischargeCapacity += storageList[j].getDischargeCapacity(time);
            }
            return dischargeCapacity;
        }

        //充電
        /// <summary>
        /// EVに充電を行う
        /// </summary>
        /// <param name="time">時</param>
        /// <param name="Energy">要求充電量</param>
        /// <returns>充電キャパシティが足りず充電できなかった量</returns>
        public double ChargeBatteries(int time, double Energy)//Energyは負の数想定
        {
            //Console.WriteLine("Before Charge : " + time + " : " + Energy);
            if (Energy == 0) { return 0; }

            double retEnergy = Energy;

            //充電優先は蓄電池＞EV
            //for文を入れ替えれば変更可

            //蓄電池の補助としてのEVか、EVの補助としてのEVかによってシナリオが変わる？
            for(int j = 0; j < storageList.Count; j++)
            {
                retEnergy = storageList[j].Charge(time, retEnergy);
                //Console.WriteLine("After Charge : " + time + " : " + retEnergy);
                if (retEnergy == 0) break;
            }

            //充電給電のソートで結果が変わる。データ取ってくる段階でどちらも対応するようにはできない
            //帰るのが遅い人優先で充電するのが当然か
            //出発時点を先に取ってくるなら、データベースのクエリの方で対応
            evList.Sort();
            evList.Reverse();

            for (int i = 0; i < evList.Count; i++)
            {
                retEnergy = evList[i].Charge(time, retEnergy);
                if (retEnergy == 0) break;
            }
            return retEnergy;
        }

        //給電
        /// <summary>
        /// EVから給電を行う
        /// </summary>
        /// <param name="time">時</param>
        /// <param name="Energy">要求給電量</param>
        /// <returns>給電ポテンシャルが足りず給電できなかった量</returns>
        public double DischargeBatteries(int time, double Energy)//Energyは正の数想定
        {
            //Console.WriteLine("Before Discharge : " + time + " : " + Energy);
            if (Energy == 0) { return 0; }

            double retEnergy = Energy;

            //給電優先も蓄電池＞EV
            //for文を入れ替えれば変更可

            //蓄電池の補助としてのEVか、EVの補助としてのEVかによってシナリオが変わる？
            for (int j = 0; j < storageList.Count; j++)
            {
                retEnergy = storageList[j].Discharge(time, retEnergy);
                //Console.WriteLine("After Discharge : " + time + " : " + retEnergy);
                if (retEnergy == 0) break;
            }

            //こちらは帰るのが早い人から優先的に給電する
            evList.Sort();
            for (int i = 0; i < evList.Count; i++)
            {
                retEnergy = evList[i].Discharge(time, retEnergy);
                if (retEnergy == 0) break;
            }
            return retEnergy;
        }
    }
}
