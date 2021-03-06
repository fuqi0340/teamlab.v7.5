/* 
 * 
 * (c) Copyright Ascensio System Limited 2010-2014
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 * 
 * http://www.gnu.org/licenses/agpl.html 
 * 
 */

#region Import

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using ASC.Collections;
using ASC.Core.Common.Logging;
using ASC.Web.CRM.Classes;
using ASC.Common.Data.Sql;
using ASC.Common.Data.Sql.Expressions;
using ASC.CRM.Core.Entities;

#endregion

namespace ASC.CRM.Core.Dao
{

    public class CachedListItem : ListItemDao
    {

        #region Members

        private readonly HttpRequestDictionary<ListItem> _listItemCache = new HttpRequestDictionary<ListItem>("crm_list_item");

        #endregion

        #region Constructor

        public CachedListItem(int tenantID, String storageKey)
            : base(tenantID, storageKey)
        {


        }

        #endregion

        #region Members

        public override void ChangeColor(int id, string newColor)
        {
            ResetCache(id);

            base.ChangeColor(id, newColor);
        }

        public override void DeleteItem(ListType listType, int itemID)
        {
            ResetCache(itemID);

            base.DeleteItem(listType, itemID);
        }

        public override void ChangePicture(int id, string newPicture)
        {
            ResetCache(id);

            base.ChangePicture(id, newPicture);
        }

        public override void EditItem(ListType listType, ListItem enumItem)
        {
            ResetCache(enumItem.ID);

            base.EditItem(listType, enumItem);
        }

        public override void ReorderItems(ListType listType, string[] titles)
        {
            _listItemCache.Clear();

            base.ReorderItems(listType, titles);
        }

        public override ListItem GetByID(int id)
        {
            return _listItemCache.Get(id.ToString(), () => GetByIDBase(id));
        }

        private ListItem GetByIDBase(int id)
        {
            return base.GetByID(id);
        }

        private void ResetCache(int id)
        {
            _listItemCache.Reset(id.ToString());
        }

        #endregion


    }

    public class ListItemDao : AbstractDao
    {
        #region Constructor

        public ListItemDao(int tenantID, String storageKey)
            : base(tenantID, storageKey)
        {


        }

        #endregion



        public bool IsExist(ListType listType, String title)
        {

            using (var db = GetDb())
            {
                return db.ExecuteScalar<int>(Query("crm_list_item").SelectCount()
                                                   .Where(Exp.Eq("list_type", (int)listType) & Exp.Eq("title", title))) > 0;
            }
        }

        public bool IsExist(int id)
        {
            using (var db = GetDb())
            {
                return db.ExecuteScalar<int>(Query("crm_list_item").SelectCount().Where(Exp.Eq("id", id))) > 0;
            }
        }

        public List<ListItem> GetItems(ListType listType)
        {
            SqlQuery sqlQuery = GetListItemSqlQuery(Exp.Eq("list_type", (int)listType))
                               .OrderBy("sort_order", true);

            using (var db = GetDb())
            {
                return db.ExecuteList(sqlQuery)
                     .ConvertAll(row => ToListItem(row));
            }
        }

        public ListItem GetSystemListItem(int id)
        {
            switch (id)
            {
                case (int)HistoryCategorySystem.TaskClosed:
                    return new ListItem
                               {
                                   ID = -1,
                                   Title = HistoryCategorySystem.TaskClosed.ToLocalizedString(),
                                   AdditionalParams = "event_category_close.png"
                               };
                case (int)HistoryCategorySystem.FilesUpload:
                    return new ListItem
                               {
                                   ID = -2,
                                   Title = HistoryCategorySystem.FilesUpload.ToLocalizedString(),
                                   AdditionalParams = "event_category_attach_file.png"
                               };
                case (int)HistoryCategorySystem.MailMessage:
                    return  new ListItem
                        {
                            ID = -3,
                            Title = HistoryCategorySystem.MailMessage.ToLocalizedString(),
                            AdditionalParams = "event_category_email.png"
                        };
                default:
                    return null;
            }

        }

        public List<ListItem> GetSystemItems()
        {
            return new List<ListItem>()
            {
                new ListItem
                        {
                            ID = (int)HistoryCategorySystem.TaskClosed,
                            Title = HistoryCategorySystem.TaskClosed.ToLocalizedString(),
                            AdditionalParams = "event_category_close.png"
                        },
                new ListItem
                        {
                            ID = (int)HistoryCategorySystem.FilesUpload,
                            Title = HistoryCategorySystem.FilesUpload.ToLocalizedString(),
                            AdditionalParams = "event_category_attach_file.png"
                        },
                new ListItem
                        {
                            ID =(int)HistoryCategorySystem.MailMessage,
                            Title = HistoryCategorySystem.MailMessage.ToLocalizedString(),
                            AdditionalParams = "event_category_email.png"
                        }
            };
        }

        public virtual ListItem GetByID(int id)
        {
            if (id < 0) return GetSystemListItem(id);

            using (var db = GetDb())
            {
                var result = db.ExecuteList(GetListItemSqlQuery(Exp.Eq("id", id))).ConvertAll(row => ToListItem(row));

                return result.Count > 0 ? result[0] : null;
            }
        }

        public virtual List<ListItem> GetItems(int[] id)
        {
            using (var db = GetDb())
            {
                var sqlResult = db.ExecuteList(GetListItemSqlQuery(Exp.In("id", id))).ConvertAll(row => ToListItem(row));

                var systemItem = id.Where(item => item < 0).Select(x => GetSystemListItem(x));

                return systemItem.Any() ? sqlResult.Union(systemItem).ToList() : sqlResult;
            }
        }

        public virtual List<ListItem> GetAll()
        {
            using (var db = GetDb())
            {
                return db.ExecuteList(GetListItemSqlQuery(null)).ConvertAll(row => ToListItem(row));
            }
        }

        public virtual void ChangeColor(int id, String newColor)
        {
            using (var db = GetDb())
            {
                db.ExecuteNonQuery(Update("crm_list_item")
                                          .Set("color", newColor)
                                          .Where(Exp.Eq("id", id)));
            }
        }

        public NameValueCollection GetColors(ListType listType)
        {

            Exp where = Exp.Eq("list_type", (int)listType);

            var result = new NameValueCollection();

            using (var db = GetDb())
            {
                db.ExecuteList(Query("crm_list_item")
                                         .Select("id", "color")
                                         .Where(where))
                                     .ForEach(row => result.Add(row[0].ToString(), row[1].ToString()));
            }
            return result;

        }

        public ListItem GetByTitle(ListType listType, String title)
        {

            using (var db = GetDb())
            {
                var result = db.ExecuteList(GetListItemSqlQuery(Exp.Eq("title", title) & Exp.Eq("list_type", (int)listType))).ConvertAll(row => ToListItem(row));

                return result.Count > 0 ? result[0] : null;
            }
        }

        public int GetRelativeItemsCount(ListType listType, int id)
        {

            SqlQuery sqlQuery;


            switch (listType)
            {
                case ListType.ContactStatus:
                    sqlQuery = Query("crm_contact")
                              .Select("count(*)")
                              .Where(Exp.Eq("status_id", id));
                    break;
                case ListType.ContactType:
                    sqlQuery = Query("crm_contact")
                              .Select("count(*)")
                              .Where(Exp.Eq("contact_type_id", id));
                    break;
                case ListType.TaskCategory:
                    sqlQuery = Query("crm_task")
                             .Select("count(*)")
                             .Where(Exp.Eq("category_id", id));
                    break;
                case ListType.HistoryCategory:
                    sqlQuery = Query("crm_relationship_event")
                              .Select("count(*)")
                              .Where(Exp.Eq("category_id", id));

                    break;
                default:
                    throw new ArgumentException();
                  
            }

            using (var db = GetDb())
            {
                return db.ExecuteScalar<int>(sqlQuery);
            }
        }

        public Dictionary<int, int> GetRelativeItemsCount(ListType listType)
        {
            var sqlQuery = Query("crm_list_item tbl_list_item")
              .Where(Exp.Eq("tbl_list_item.list_type", (int)listType))
              .Select("tbl_list_item.id")
              .OrderBy("tbl_list_item.sort_order", true)
              .GroupBy("tbl_list_item.id");

            switch (listType)
            {
                case ListType.ContactStatus:
                    sqlQuery.LeftOuterJoin("crm_contact tbl_crm_contact",
                                            Exp.EqColumns("tbl_list_item.id", "tbl_crm_contact.status_id")
                                             & Exp.EqColumns("tbl_list_item.tenant_id", "tbl_crm_contact.tenant_id"))
                                          .Select("count(tbl_crm_contact.status_id)");
                    break;
                case ListType.ContactType:
                    sqlQuery.LeftOuterJoin("crm_contact tbl_crm_contact",
                                            Exp.EqColumns("tbl_list_item.id", "tbl_crm_contact.contact_type_id")
                                             & Exp.EqColumns("tbl_list_item.tenant_id", "tbl_crm_contact.tenant_id"))
                                          .Select("count(tbl_crm_contact.contact_type_id)");
                    break;
                case ListType.TaskCategory:
                    sqlQuery.LeftOuterJoin("crm_task tbl_crm_task",
                                            Exp.EqColumns("tbl_list_item.id", "tbl_crm_task.category_id")
                                              & Exp.EqColumns("tbl_list_item.tenant_id", "tbl_crm_task.tenant_id"))
                                           .Select("count(tbl_crm_task.category_id)");
                    break;
                case ListType.HistoryCategory:
                    sqlQuery.LeftOuterJoin("crm_relationship_event tbl_crm_relationship_event",
                                            Exp.EqColumns("tbl_list_item.id", "tbl_crm_relationship_event.category_id")
                                              & Exp.EqColumns("tbl_list_item.tenant_id", "tbl_crm_relationship_event.tenant_id"))
                                           .Select("count(tbl_crm_relationship_event.category_id)");

                    break;
                default:
                    throw new ArgumentException();
            }

            using (var db = GetDb())
            {
                var queryResult = db.ExecuteList(sqlQuery);

                return queryResult.ToDictionary(x => Convert.ToInt32(x[0]), y => Convert.ToInt32(y[1]));
            }
        }

        public virtual int CreateItem(ListType listType, ListItem enumItem)
        {

            if (IsExist(listType, enumItem.Title))
                return GetByTitle(listType, enumItem.Title).ID;

            if (String.IsNullOrEmpty(enumItem.Title))
                throw new ArgumentException();

            if (listType == ListType.TaskCategory || listType == ListType.HistoryCategory)
                if (String.IsNullOrEmpty(enumItem.AdditionalParams))
                    throw new ArgumentException();
                else
                   enumItem.AdditionalParams = System.IO.Path.GetFileName(enumItem.AdditionalParams);
                
            if (listType == ListType.ContactStatus)
                if (String.IsNullOrEmpty(enumItem.Color))
                    throw new ArgumentException();

            var sortOrder = enumItem.SortOrder;

            using (var db = GetDb())
            {
                if (sortOrder == 0)
                    sortOrder = db.ExecuteScalar<int>(Query("crm_list_item")
                                                            .Where(Exp.Eq("list_type", (int)listType))
                                                            .SelectMax("sort_order")) + 1;

                AdminLog.PostAction("CRM: saved crm category of type \"{0}\" with parameters {1:Json}", listType, enumItem);

                return db.ExecuteScalar<int>(
                                                  Insert("crm_list_item")
                                                  .InColumnValue("id", 0)
                                                  .InColumnValue("list_type", (int)listType)
                                                  .InColumnValue("description", enumItem.Description)
                                                  .InColumnValue("title", enumItem.Title)
                                                  .InColumnValue("additional_params", enumItem.AdditionalParams)
                                                  .InColumnValue("color", enumItem.Color)
                                                  .InColumnValue("sort_order", sortOrder)
                                                  .Identity(1, 0, true));
            }
        }

        public virtual void EditItem(ListType listType, ListItem enumItem)
        {

            if (HaveRelativeItemsLink(listType, enumItem.ID))
                throw new ArgumentException();

            using (var db = GetDb())
            {
                db.ExecuteNonQuery(Update("crm_list_item")
                                         .Set("description", enumItem.Description)
                                         .Set("title", enumItem.Title)
                                         .Set("additional_params", enumItem.AdditionalParams)
                                         .Set("color", enumItem.Color)
                                         .Where(Exp.Eq("id", enumItem.ID)));
            }

            AdminLog.PostAction("CRM: saved crm category of type \"{0}\" with parameters {1:Json}", listType, enumItem);
        }

        public virtual void ChangePicture(int id, String newPicture)
        {
            using (var db = GetDb())
            {
                db.ExecuteNonQuery(Update("crm_list_item")
                                     .Set("additional_params", newPicture)
                                     .Where(Exp.Eq("id", id)));
            }
        }

        private bool HaveRelativeItemsLink(ListType listType, int itemID)
        {
            SqlQuery sqlQuery;

            switch (listType)
            {

                case ListType.ContactStatus:
                    sqlQuery = Query("crm_contact")
                               .Where(Exp.Eq("status_id", itemID));
                    break;
                case ListType.ContactType:
                    sqlQuery = Query("crm_contact")
                                .Where(Exp.Eq("contact_type_id", itemID));

                    break;
                case ListType.TaskCategory:
                    sqlQuery = Query("crm_task")
                              .Where(Exp.Eq("category_id", itemID));
                    break;
                case ListType.HistoryCategory:
                    sqlQuery = Query("crm_relationship_event")
                              .Where(Exp.Eq("category_id", itemID));
                    break;
                default:
                    throw new ArgumentException();
            }

            using (var db = GetDb())
            {
                return db.ExecuteScalar<int>(sqlQuery.SelectCount()) > 0;
            }
        }

        public void ChangeRelativeItemsLink(ListType listType, int fromItemID, int toItemID)
        {


            if (!IsExist(fromItemID))
                throw new ArgumentException("", "toItemID");
           
            if (!HaveRelativeItemsLink(listType, fromItemID)) return;

            if (!IsExist(toItemID))
                throw new ArgumentException("", "toItemID");
           
            SqlUpdate sqlUpdate;
            
            switch (listType)
            {
                case ListType.ContactStatus:
                    sqlUpdate = Update("crm_contact")
                                .Set("status_id", toItemID)
                                .Where(Exp.Eq("status_id", fromItemID));
                    break;
                case ListType.ContactType:
                    sqlUpdate = Update("crm_contact")
                                .Set("contact_type_id", toItemID)
                                .Where(Exp.Eq("contact_type_id", fromItemID));
                    break;
                case ListType.TaskCategory:
                    sqlUpdate = Update("crm_task")
                               .Set("category_id", toItemID)       
                               .Where(Exp.Eq("category_id", fromItemID));
                    break;
                case ListType.HistoryCategory:
                    sqlUpdate = Update("crm_relationship_event")
                               .Set("category_id", toItemID)  
                               .Where(Exp.Eq("category_id", fromItemID));
                    break;
                default:
                    throw new ArgumentException();
            }

            using (var db = GetDb())
            {
                db.ExecuteNonQuery(sqlUpdate);
            }
        }

        public virtual void DeleteItem(ListType listType, int itemID)
        {
             
            if (HaveRelativeItemsLink(listType, itemID))
                throw new ArgumentException();

            using (var db = GetDb())
            {
                db.ExecuteNonQuery(Delete("crm_list_item").Where(Exp.Eq("id", itemID) & Exp.Eq("list_type", (int)listType)));
            }

            AdminLog.PostAction("CRM: deleted crm category of type \"{0}\" having ID \"{1}\"", listType, itemID);
        }

        public virtual void ReorderItems(ListType listType, String[] titles)
        {
            using (var db = GetDb())
            using (var tx = db.BeginTransaction())
            {
                for (int index = 0; index < titles.Length; index++)
                    db.ExecuteNonQuery(Update("crm_list_item")
                                             .Set("sort_order", index)
                                             .Where(Exp.Eq("title", titles[index]) & Exp.Eq("list_type", (int)listType)));

                tx.Commit();
            }
        }

        private SqlQuery GetListItemSqlQuery(Exp where)
        {
            var result = Query("crm_list_item")
               .Select(
                   "id",
                   "title",
                   "description",
                   "color",
                   "sort_order",
                   "additional_params",
                   "list_type"
               );

            if (where != null)
                result.Where(where);

            return result;

        }

        public static ListItem ToListItem(object[] row)
        {
            return new ListItem
                       {
                           ID = Convert.ToInt32(row[0]),
                           Title = Convert.ToString(row[1]),
                           Description = Convert.ToString(row[2]),
                           Color = Convert.ToString(row[3]),
                           SortOrder = Convert.ToInt32(row[4]),
                           AdditionalParams = Convert.ToString(row[5])
                       };
        }
    }
}
