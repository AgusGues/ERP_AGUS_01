using Microsoft.Data.SqlClient;
using System.Data;

namespace ERP_AGUS_01.Data
{
    public class DbHelper
    {
        private readonly string _connectionString;

        public DbHelper(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
        }

        // =========================
        // 🔹 CONNECTION CONTROL
        // =========================
        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        // =========================
        // 🔹 QUERY (NO TRANSACTION)
        // =========================
        public DataTable ExecuteQuery(string sql, SqlParameter[] parameters = null)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            if (parameters != null)
                cmd.Parameters.AddRange(parameters);

            using var da = new SqlDataAdapter(cmd);
            var dt = new DataTable();
            da.Fill(dt);
            return dt;
        }

        public object ExecuteScalar(string sql, SqlParameter[] parameters = null)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            if (parameters != null)
                cmd.Parameters.AddRange(parameters);

            conn.Open();
            return cmd.ExecuteScalar();
        }

        public int ExecuteNonQuery(string sql, SqlParameter[] parameters = null)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            if (parameters != null)
                cmd.Parameters.AddRange(parameters);

            conn.Open();
            return cmd.ExecuteNonQuery();
        }

        // =========================
        // 🔹 TRANSACTION SAFE
        // =========================
        public DataTable ExecuteQuery(
            string sql,
            SqlParameter[] parameters,
            SqlConnection conn,
            SqlTransaction tran)
        {
            using var cmd = new SqlCommand(sql, conn, tran);
            if (parameters != null)
                cmd.Parameters.AddRange(parameters);

            using var da = new SqlDataAdapter(cmd);
            var dt = new DataTable();
            da.Fill(dt);
            return dt;
        }

        public object ExecuteScalar(
            string sql,
            SqlParameter[] parameters,
            SqlConnection conn,
            SqlTransaction tran)
        {
            using var cmd = new SqlCommand(sql, conn, tran);
            if (parameters != null)
                cmd.Parameters.AddRange(parameters);

            return cmd.ExecuteScalar();
        }

        public int ExecuteNonQuery(
            string sql,
            SqlParameter[] parameters,
            SqlConnection conn,
            SqlTransaction tran)
        {
            using var cmd = new SqlCommand(sql, conn, tran);
            if (parameters != null)
                cmd.Parameters.AddRange(parameters);

            return cmd.ExecuteNonQuery();
        }
    }
}
