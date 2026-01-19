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

        public DataTable ExecuteQuery(string sql, SqlParameter[] parameters = null)
        {
            using SqlConnection conn = new(_connectionString);
            using SqlCommand cmd = new(sql, conn);
            if (parameters != null)
                cmd.Parameters.AddRange(parameters);

            using SqlDataAdapter da = new(cmd);
            DataTable dt = new();
            da.Fill(dt);
            return dt;
        }

        public int ExecuteNonQuery(string sql, SqlParameter[] parameters = null)
        {
            using SqlConnection conn = new(_connectionString);
            conn.Open();
            using SqlCommand cmd = new(sql, conn);
            if (parameters != null)
                cmd.Parameters.AddRange(parameters);
            return cmd.ExecuteNonQuery();
        }

        public object ExecuteScalar(string sql, SqlParameter[] parameters = null)
        {
            using SqlConnection conn = new(_connectionString);
            conn.Open();
            using SqlCommand cmd = new(sql, conn);
            if (parameters != null)
                cmd.Parameters.AddRange(parameters);
            return cmd.ExecuteScalar();
        }

    }
}
