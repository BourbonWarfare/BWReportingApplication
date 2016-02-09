﻿using MySql.Data;
using MySql.Data.MySqlClient;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Globalization;

using BWServerLogger.Model;
using BWServerLogger.Util;

namespace BWServerLogger.DAO
{
    public class ScheduleDAO
    {
        private MySqlConnection _connection;

        private MySqlCommand _addScheduleItem;
        private MySqlCommand _getScheduleItems;
        private MySqlCommand _removeScheduleItem;
        private MySqlCommand _updateScheduleItem;

        public ScheduleDAO(MySqlConnection connection)
        {
            _connection = connection;
            SetupPreparedStatements();
        }

        public ISet<Schedule> GetScheduleItems()
        {
            ISet<Schedule> scheduleItems = null;

            MySqlDataReader getScheduleItemsResult = _getScheduleItems.ExecuteReader();

            if (getScheduleItemsResult.HasRows)
            {
                scheduleItems = new HashSet<Schedule>();
                while (getScheduleItemsResult.Read())
                {
                    scheduleItems.Add(new Schedule(getScheduleItemsResult.GetInt32(0),
                                                   getScheduleItemsResult.GetInt32(1),
                                                   getScheduleItemsResult.GetString(2)));
                }
            }

            getScheduleItemsResult.Close();

            return scheduleItems;
        }

        public void SaveScheduleItem(Schedule scheduleItem)
        {
            if (scheduleItem.Id < 1)
            {
                _addScheduleItem.Parameters[DatabaseUtil.DAY_OF_THE_WEEK_KEY].Value = scheduleItem.DayOfTheWeek + 1;
                _addScheduleItem.Parameters[DatabaseUtil.TIME_OF_DAY_KEY].Value = scheduleItem.TimeOfDay.ToString();
                _addScheduleItem.ExecuteNonQuery();
            }
            else
            {
                _updateScheduleItem.Parameters[DatabaseUtil.DAY_OF_THE_WEEK_KEY].Value = scheduleItem.DayOfTheWeek + 1;
                _updateScheduleItem.Parameters[DatabaseUtil.TIME_OF_DAY_KEY].Value = scheduleItem.TimeOfDay.ToString();
                _updateScheduleItem.Parameters[DatabaseUtil.SCHEDULE_ID_KEY].Value = scheduleItem.Id;
                _updateScheduleItem.ExecuteNonQuery();
            }
        }

        public void RemoveScheduleItem(Schedule scheduleItem)
        {
            _removeScheduleItem.Parameters[DatabaseUtil.SCHEDULE_ID_KEY].Value = scheduleItem.Id;
            _removeScheduleItem.ExecuteNonQuery();
        }

        private void SetupPreparedStatements()
        {
            StringBuilder getScheduleItemsSelect = new StringBuilder();
            getScheduleItemsSelect.Append("select id, day_of_the_week-1, time_of_day ");
            getScheduleItemsSelect.Append("from schedule ");

            _getScheduleItems = new MySqlCommand(getScheduleItemsSelect.ToString(), _connection);
            _getScheduleItems.Prepare();

            StringBuilder addScheduleItemInsert = new StringBuilder();
            addScheduleItemInsert.Append("insert into schedule (day_of_the_week, time_of_day)");
            addScheduleItemInsert.Append("values (");
            addScheduleItemInsert.Append(DatabaseUtil.DAY_OF_THE_WEEK_KEY);
            addScheduleItemInsert.Append(", ");
            addScheduleItemInsert.Append(DatabaseUtil.TIME_OF_DAY_KEY);
            addScheduleItemInsert.Append(")");


            _addScheduleItem = new MySqlCommand(addScheduleItemInsert.ToString(), _connection);
            _addScheduleItem.Parameters.Add(new MySqlParameter(DatabaseUtil.DAY_OF_THE_WEEK_KEY, MySqlDbType.Int32));
            _addScheduleItem.Parameters.Add(new MySqlParameter(DatabaseUtil.TIME_OF_DAY_KEY, MySqlDbType.String));
            _addScheduleItem.Prepare();

            StringBuilder scheduleItemUpdate = new StringBuilder();
            scheduleItemUpdate.Append("update schedule ");
            scheduleItemUpdate.Append("set day_of_the_week = ");
            scheduleItemUpdate.Append(DatabaseUtil.DAY_OF_THE_WEEK_KEY);
            scheduleItemUpdate.Append(", ");
            scheduleItemUpdate.Append("time_of_day = ");
            scheduleItemUpdate.Append(DatabaseUtil.TIME_OF_DAY_KEY);
            scheduleItemUpdate.Append(" ");
            scheduleItemUpdate.Append("where id = ");
            scheduleItemUpdate.Append(DatabaseUtil.SCHEDULE_ID_KEY);

            _updateScheduleItem = new MySqlCommand(scheduleItemUpdate.ToString(), _connection);
            _updateScheduleItem.Parameters.Add(new MySqlParameter(DatabaseUtil.DAY_OF_THE_WEEK_KEY, MySqlDbType.Int32));
            _updateScheduleItem.Parameters.Add(new MySqlParameter(DatabaseUtil.TIME_OF_DAY_KEY, MySqlDbType.String));
            _updateScheduleItem.Parameters.Add(new MySqlParameter(DatabaseUtil.SCHEDULE_ID_KEY, MySqlDbType.Int32));
            _updateScheduleItem.Prepare();

            StringBuilder scheduleItemDelete = new StringBuilder();
            scheduleItemDelete.Append("delete from schedule ");
            scheduleItemDelete.Append("where id = ");
            scheduleItemDelete.Append(DatabaseUtil.SCHEDULE_ID_KEY);

            _removeScheduleItem = new MySqlCommand(scheduleItemDelete.ToString(), _connection);
            _removeScheduleItem.Parameters.Add(new MySqlParameter(DatabaseUtil.SCHEDULE_ID_KEY, MySqlDbType.Int32));
            _removeScheduleItem.Prepare();
        }

    }
}