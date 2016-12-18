using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows.Forms;

namespace MicroGridSample
{
    public partial class MainView : Form
    {
        public MainView()
        {
            InitializeComponent();
        }

        #region データセット作成メソッド
        private MicroGridBattery GetMicroGridBattery(List<DateTime> evListDate, int pattern, int number, string order, double storageCapacity) //マイクログリッドのバッテリ（EV・蓄電池）クラス生成
        {
            //引数
            //evListDate ： EVデータセットの日付（総研41台は2016/01/20・常盤台キャンパス559台であれば2016/11/04を指定）
            //pattern ： 旧クエリにおけるデータセットパターン（0.8kWh～6.8kWhランダムは"0"・実データは"50"）
            //number ： EV台数
            //order ： 充給電EVの優先度（ORDER BY句）
            //storageCapacity ： 据え置き蓄電池の容量

            MicroGridBattery MGB = new MicroGridBattery();

            //旧クエリ
            //もともとはTRIPS_TESTに対してクエリを投げていたが
            //500台ログはYNU_Tripsテーブルに挿入したため変更
            //もし旧データ（41台・2016年夏のLeaf実験データ）等を使用する場合は
            //旧クエリに変更する（pattern = 0 or 50）

            #region 旧クエリ
            //クエリでデータセットに条件をつけること（例：往路の消費の多い車10件、帰りの遅い車20件）
            //string query = "SELECT TOP " + number + " CarID, OutEndTime, HomeStartTime, OutEnergy, HomeEnergy " + "\r\n";
            //query += "FROM TRIPS_TEST " + "\r\n";
            //query += "WHERE Date in ('" + evListDate[0].ToShortDateString() + "'";
            //for (int i = 1; i < evListDate.Count; i++)
            //{
            //    query += ",'" + evListDate[i].ToShortDateString() + "'";
            //}
            //query += ") \r\n";
            //query += "AND EnergyPatternID = " + pattern + " " + "\r\n";
            //query +=
            //query += "\r\n";
            #endregion

            //新クエリ
            //引数に指定された台数を引数に指定された条件だけ抽出するクエリ

            #region 新クエリ
            string query = "SELECT TOP " + number + " CarID, OutEndTime, HomeStartTime, OutEnergy, HomeEnergy " + Environment.NewLine;
            query += "FROM YNU_Trips " + Environment.NewLine;
            query += "WHERE Date in ('" + evListDate[0].ToShortDateString() + "'";
            for (int i = 1; i < evListDate.Count; i++)
            {
                query += ",'" + evListDate[i].ToShortDateString() + "'";
            }
            query += ") " + Environment.NewLine;
            query += order + Environment.NewLine;
            #endregion

            int carID;
            EVBattery ev;
            StorageBattery sb;
            DateTime arrive, departure;
            double outEnergy, homeEnergy;

            //StorageBatteryは引数に容量[kWh]を指定
            sb = new StorageBattery(storageCapacity);
            //MicroGridBatteryの構成要素としてAdd
            MGB.AddStorage(sb);

            try
            {
                sqlConnection1.Open();
                SqlCommand cmd = new SqlCommand(query, sqlConnection1);
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    carID = (int)reader[0];
                    arrive = (DateTime)reader[1];
                    departure = (DateTime)reader[2];
                    outEnergy = (double)reader[3];
                    homeEnergy = (double)reader[4];

                    //EVBatteryはCarID, 到着時間, 出発時間, outwardの消費エネルギー, homewardの消費エネルギーを引数に持つ
                    ev = new EVBattery(carID, arrive, departure, outEnergy, homeEnergy);
                    //MicroGridBatteryの構成要素としてAdd
                    MGB.AddEV(ev);
                }
            }
            catch (Exception)
            {
            }

            finally
            {
                sqlConnection1.Close();
            }

            return MGB;
        }

        private MicroGridOneday GetMicroGridOneday(DateTime dt, List<int> bID, List<int> pID, MicroGridBattery mgb, int peakPattern, double rate) //マイクログリッドの1日の推移クラス生成
        {
            //引数
            //dt ： 需要・発電ログの日時
            //bID ： 横国見える化の区画ID
            //pID ： 駐車場・駐輪場ID
            //mgb ： MicroGridBattery
            //peakPattern ： 電力ピークのパターン（最大±1hは"0"・最大±2hは"1"・大きい順2hは"2"・大きい順3hは"3"・大きい順4hは"4"）
            //rate ： 太陽光設置率

            MicroGridOneday MGO; //返り値用

            double square = 0; //対象の建物, 駐車場・駐輪場の面積（MicroGridOnedayのコンストラクタには必要, 計算には使わないが）
            double[] consume = new double[24];
            double[] generate = new double[24];
            double[] difference = new double[24]; //電力需要（1時間毎）, 電力発電（1時間毎）, 差分
            int[] tepco = new int[24]; //TEPCO需要電力[MW]

            string query;
            SqlCommand cmd;
            SqlDataReader reader;
            int index = 0; //配列用インデックス

            //以降各コンポーネントごとの取得処理を1つにまとめているためメソッドがとてつもなく長くなっているが
            //本来はもう少しコンパクトに書くべき（取得データごとにメソッドを分けるとか）
            #region squareの取得

            #region クエリ

            if (pID.Count > 0) //駐輪場の指定あり
            {
                query = "SELECT bArea.BuildingConstructionArea + pArea.ParkingConstructionArea " + Environment.NewLine;
                query += "FROM " + Environment.NewLine;
                query += "( " + Environment.NewLine;
                query += "	SELECT SUM(ConstructionArea) BuildingConstructionArea " + Environment.NewLine;
                query += "	FROM Building " + Environment.NewLine;
                query += "  WHERE MeasureAreaClassID IN (" + bID[0];
                for (int i = 1; i < bID.Count; i++)
                {
                    query += ", " + bID[i] + "";
                }
                query += ") " + Environment.NewLine;
                query += "	GROUP BY MeasureAreaClassID " + Environment.NewLine;
                query += ") bArea, " + Environment.NewLine;
                query += "( " + Environment.NewLine;
                query += "	SELECT SUM(ConstructionArea) ParkingConstructionArea " + Environment.NewLine;
                query += "	FROM Parking " + Environment.NewLine;
                query += "  WHERE ID IN (" + bID[0];
                for (int i = 1; i < pID.Count; i++)
                {
                    query += ", " + pID[i] + "";
                }
                query += ") " + Environment.NewLine;
                query += "	GROUP BY ID " + Environment.NewLine;
                query += ") pArea " + Environment.NewLine;
            }
            else //駐輪場の指定なし
            {
                query = "SELECT SUM(ConstructionArea) " + Environment.NewLine;
                query += "FROM Building " + Environment.NewLine;
                query += "WHERE MeasureAreaClassID IN (" + bID[0];
                for (int i = 1; i < bID.Count; i++)
                {
                    query += ", " + bID[i] + "";
                }
                query += ") " + Environment.NewLine;
                query += "GROUP BY MeasureAreaClassID " + Environment.NewLine;
            }
            #endregion

            try
            {
                sqlConnection1.Open();
                cmd = new SqlCommand(query, sqlConnection1);
                reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    square = (double)reader.GetInt32(0);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                sqlConnection1.Close();
            }

            #endregion

            #region consumeの取得

            #region クエリ

            query = "SELECT SUM(Energy) " + Environment.NewLine;
            query += "FROM BuildingPowerLog " + Environment.NewLine;
            query += "WHERE MeasureAreaClassID IN (" + bID[0];
            for (int i = 1; i < bID.Count; i++)
            {
                query += ", " + bID[i] + "";
            }
            query += ") " + Environment.NewLine;
            query += "AND CONVERT(Date, Date, 111) = '" + dt.ToShortDateString() + "' " + Environment.NewLine;
            query += "GROUP BY Date " + Environment.NewLine;
            query += "ORDER BY Date " + Environment.NewLine;

            #endregion

            try
            {
                sqlConnection1.Open();
                cmd = new SqlCommand(query, sqlConnection1);
                reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    consume[index] = (double)reader[0];
                    index++;
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                sqlConnection1.Close();
            }

            #endregion

            #region generateの取得

            index = 0;

            #region クエリ

            if (pID.Count > 0) //駐輪場の指定あり
            {
                query = "SELECT ROUND(bLog.bGenerateEnergy + pLog.pGenerateEnergy, 1) " + Environment.NewLine;
                query += "FROM " + Environment.NewLine;
                query += "( " + Environment.NewLine;
                query += "	SELECT Date, SUM(GenerateEnergy) bGenerateEnergy " + Environment.NewLine;
                query += "	FROM BuildingSolarPowerLog " + Environment.NewLine;
                query += "WHERE MeasureAreaClassID IN (" + bID[0];
                for (int i = 1; i < bID.Count; i++)
                {
                    query += ", " + bID[i] + "";
                }
                query += ") " + Environment.NewLine;
                query += "	AND CONVERT(Date, Date, 111) = '" + dt.ToShortDateString() + "' " + Environment.NewLine;
                query += "	GROUP BY Date " + Environment.NewLine;
                query += ")bLog, " + Environment.NewLine;
                query += "( " + Environment.NewLine;
                query += "	SELECT Date, SUM(GenerateEnergy) pGenerateEnergy " + Environment.NewLine;
                query += "	FROM ParkingSolarPowerLog " + Environment.NewLine;
                query += "  WHERE ID IN (" + pID[0];
                for (int i = 1; i < pID.Count; i++)
                {
                    query += ", " + pID[i] + "";
                }
                query += ") " + Environment.NewLine;
                query += "	AND CONVERT(Date, Date, 111) = '" + dt.ToShortDateString() + "' " + Environment.NewLine;
                query += "	GROUP BY Date " + Environment.NewLine;
                query += ")pLog " + Environment.NewLine;
                query += "WHERE bLog.Date = pLog.Date " + Environment.NewLine;
                query += "ORDER BY bLog.Date " + Environment.NewLine;
            }
            else
            {
                query = "SELECT SUM(GenerateEnergy) " + Environment.NewLine;
                query += "FROM BuildingSolarPowerLog " + Environment.NewLine;
                query += "WHERE MeasureAreaClassID IN (" + bID[0];
                for (int i = 1; i < bID.Count; i++)
                {
                    query += ", " + bID[i] + "";
                }
                query += ") " + Environment.NewLine;
                query += "AND CONVERT(Date, Date, 111) = '" + dt.ToShortDateString() + "' " + Environment.NewLine;
                query += "GROUP BY Date " + Environment.NewLine;
                query += "ORDER BY Date " + Environment.NewLine;
            }

            #endregion

            try
            {
                sqlConnection1.Open();
                cmd = new SqlCommand(query, sqlConnection1);
                reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    generate[index] = (double)reader[0] * rate;
                    index++;
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                sqlConnection1.Close();
            }

            #endregion

            #region differenceの取得

            for(int i = 0; i < 24; i++)
            {
                difference[i] = consume[i] - generate[i];
            }

            #endregion

            #region tepcoの取得

            index = 0;

            #region クエリ

            query = "SELECT Energy * 10 " + Environment.NewLine;
            query += "FROM TEPCODemandData " + Environment.NewLine;
            query += "WHERE CONVERT(Date, Date, 111) = '" + dt.ToShortDateString() + "' " + Environment.NewLine;
            query += "ORDER BY Date " + Environment.NewLine;

            #endregion

            try
            {
                sqlConnection1.Open();
                cmd = new SqlCommand(query, sqlConnection1);
                reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    tepco[index] = (int)reader.GetDouble(0);
                    index++;
                }
            }
            catch (Exception)
            { 
            }
            finally
            {
                sqlConnection1.Close();
            }

            #endregion

            MGO = new MicroGridOneday(square, consume, generate, difference, tepco, mgb, peakPattern);

            return MGO;

        }
        #endregion

        private void button1_Click(object sender, EventArgs e)
        {
            List<DateTime> evListDate = new List<DateTime>();
            int evPattern;
            int number;
            string order;
            double storageCapacity;

            evListDate.Add(DateTime.Parse("2016/11/04")); //559台分のEVデータセット日時
            evPattern = 0; //EVのエネルギー割当パターン：新クエリでは不使用
            number = 559; //EVの台数
            order = "ORDER BY OutEndTime ASC "; //EVの優先度
            storageCapacity = 10; //蓄電池容量[kWh]

            MicroGridBattery mgb = GetMicroGridBattery(evListDate, evPattern, number, order, storageCapacity); //グリッドの蓄電池を定義

            Console.WriteLine("-- 充キャパ・給ポテ --");
            for (int i = 0; i < 24; i++)
            {
                Console.WriteLine(i + " : " + mgb.GetAllChargeCapacity(i) + " " + mgb.GetAllDischargeCapacity(i));
            }
            Console.WriteLine("-- 充キャパ・給ポテ --");

            DateTime dt;
            List<int> bID = new List<int>();
            List<int> pID = new List<int>(); //駐車場・駐輪場ID：本例では不使用
            int peakPattern;
            double rate;

            dt = DateTime.Parse("2015/07/31"); //マイクログリッドの対象日時
            for (int i = 1; i <= 17; i++)
            {
                bID.Add(i); //建物ID：ここでは例として横国の建物すべてを対象とする
            }
            peakPattern = 0; //最大±1h
            rate = 1.0; //太陽光設置率100%

            MicroGridOneday mgo = GetMicroGridOneday(dt, bID, pID, mgb, peakPattern, rate);

            Console.WriteLine("-- 1日の需要・発電・差分・バッテリー導入差分・東電・ピークかどうか --");
            for (int i = 0; i < 24; i++)
            {
                Console.WriteLine(i + " : " + mgo.GetPowerLog(i) + " " + mgo.GetGeneration(i) + " " + mgo.GetDifferencePG(i) + " " + mgo.GetDifferencePGEV(i) + " " + mgo.GetDemand(i) + " " + mgo.Peaktime[i]);
            }
            Console.WriteLine("-- 1日の需要・発電・差分・バッテリー導入差分・東電・ピークかどうか --");

            //1時間ごとのPV余剰量やEVへの充電量・給電量は計算で導出可（以下）
            Console.WriteLine("-- 1日のPVの余剰量・EVへの充電量・EVからの給電量 --");
            for (int i = 0; i < 24; i++)
            {
                double overGeneration, EVCharge, EVDischarge;
                
                if(mgo.GetDifferencePG(i) < 0) //PV発電超過
                {
                    overGeneration = -mgo.GetDifferencePG(i); //需要・発電相殺後の値（発電超過だと負の値になるため符号をひっくり返す）
                    EVCharge = mgo.GetDifferencePGEV(i) - mgo.GetDifferencePG(i); //EV導入前の値と導入後の値の差分がバッテリーへの充電量
                }
                else
                {
                    overGeneration = 0;
                    EVCharge = 0;
                }

                if(mgo.Peaktime[i] && mgo.GetDifferencePG(i) > 0) //ピーク時間帯かつPVの発電超過ではない
                {
                    EVDischarge = mgo.GetDifferencePG(i) - mgo.GetDifferencePGEV(i); //EV導入前の値と導入後の値の差分がバッテリーからの給電量
                }
                else
                {
                    EVDischarge = 0;
                }

                Console.WriteLine(i + " : " + overGeneration + " " + EVCharge + " " + EVDischarge);
            }
            Console.WriteLine("-- 1日のPVの余剰量・EVへの充電量・EVからの給電量 --");

            Console.WriteLine("PV・EV導入前のピーク時間帯エネルギー量[kWh] : " + mgo.GetBeforePeakEnergy());
            Console.WriteLine("PV・EV導入後のピーク時間帯エネルギー量[kWh] : " + mgo.GetAfterPeakEnergy());
            Console.WriteLine("PV・EV導入前のピーク時間帯最大電力[kW] : " + mgo.GetPeakBeforeMaxWatt());
            Console.WriteLine("PV・EV導入後のピーク時間帯最大電力[kW] : " + mgo.GetPeakAfterMaxWatt());
            Console.WriteLine("PV・EV導入前の余剰発電量[kWh] : " + mgo.GetBeforeOverGeneration());
            Console.WriteLine("PV・EV導入後の余剰発電量[kWh] : " + mgo.GetAfterOverGeneration());

        }
    }
}
