using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MicroGridSample
{
    public partial class MainView : Form
    {
        public MainView()
        {
            InitializeComponent();
        }

        private MicroGridBattery GetMicroGridBattery(List<DateTime> evListDate, int pattern, int number, string order, double storageCapacity) //マイクログリッドのバッテリ（EV・蓄電池）クラス
        {
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

        private MicroGridOneday GetMicroGridOneday(DateTime dt, List<int> bID, List<int> pID, MicroGridBattery mgb, int peakPattern) //マイクログリッドの1日の推移クラス
        {
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

            //以降各コンポーネントごとの取得処理を1つにまとめているためメソッドがとてつもなくも長くなっているが
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
                    square = (int)reader[0];
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
                    consume[index] = (int)reader[0];
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
                    generate[index] = (int)reader[0];
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

            query = "SELECT Energy " + Environment.NewLine;
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
                    tepco[index] = (int)reader[0];
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
    }
}
