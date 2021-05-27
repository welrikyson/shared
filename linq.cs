public async Task<PublicCommissioningDetailsDTO[]> GetCommissioningDetailsByDatesAsync(DateTime start, DateTime end, CancellationToken ct = default)
        {
            var routes = _context.AUG_Route
                                 .Where(r => r.RouteType == (short)RouteType.Commissioning)
                                 .Where(GetRoutePrivacyFilter(this.UserID));
            var allRouteFeedbackDetails =
                await (from r in routes
                       from rf in r.AUG_RouteFeedback
                       from rfd in rf.AUG_RouteFeedbackDetail
                       where rfd.Timestamp >= start
                       where rfd.Timestamp < end
                       orderby rfd.Timestamp descending
                       select new
                       {​
                           IdInstallationLocal = rfd.IdInstallationLocal,
                           RouteFeedbackDetailID = rfd.IdRouteFeedbackDetail,
                           RouteGroupID = r.IdRouteGroup,
                           RouteID = r.IdRoute,
                           Route = r.Route,
                           RouteDescription = r.Description,
                           CheckListItemID = rfd.IdCheckListItem,
                           CheckListOptionDetailID = rfd.IdCheckListOptionDetail,
                           EditedBy = rfd.EditedBy,
                           EditedByName = rfd.EditedByNavigation.Name,
                           Timestamp = rfd.Timestamp,
                           rfd.IdRouteFeedback,
                       }​).ToArrayAsync(ct);
            if (allRouteFeedbackDetails.Length == 0)
                return Array.Empty<PublicCommissioningDetailsDTO>();
            var routeFeedbackDetails = allRouteFeedbackDetails.ToLookup(r => new {​ r.IdRouteFeedback, r.IdInstallationLocal, r.CheckListItemID }​);
            var attachments = await (from rfd in _context.AUG_RouteFeedbackDetail
                                     from rfda in rfd.AUG_RouteFeedbackDetailAttachment
                                     where rfd.Timestamp >= start
                                     where rfd.Timestamp < end
                                     select new PublicAttachmentDTO
                                     {​
                                         ID = rfda.IdAttachment,
                                         RouteFeedbackDetailID = rfda.IdRouteFeedbackDetail,
                                         Description = rfda.Description,
                                         Filename = rfda.Filename,
                                         FileByteLength = rfda.FileByteLength,
                                         Timestamp = rfda.EditedTimestamp,
                                         EditedBy = rfda.EditedBy,
                                         EditedByName = rfda.EditedByNavigation.Name
                                     }​).ToArrayAsync(ct);
            var comments = await (from rfd in _context.AUG_RouteFeedbackDetail
                                  from rfdc in rfd.AUG_RouteFeedbackDetailComments
                                  where rfd.Timestamp >= start
                                  where rfd.Timestamp < end
                                  select new PublicCommentDTO
                                  {​
                                      RouteFeedbackDetailID = rfdc.IdRouteFeedbackDetail,
                                      Content = rfdc.Comments,
                                      Timestamp = rfdc.Timestamp,
                                      EditedBy = rfdc.EditedBy,
                                      EditedByName = rfdc.EditedByNavigation.Name
                                  }​).ToArrayAsync(ct);
            //var routeGroupIDs = routeFeedbackDetails.Where(r => r.RouteGroupID.IsNotNull())
            //                                        .Select(r => r.RouteGroupID)
            //                                        .Distinct();
            var routeGroupIDs = new List<int>();
            var routeGroups = await (from routeGroup in _context.AUG_RouteGroup
                                     where routeGroupIDs.Contains(routeGroup.IdRouteGroup)
                                     select new
                                     {​
                                         routeGroup.IdRouteGroup,
                                         routeGroup.RouteGroup,
                                         routeGroup.Description
                                     }​).ToDictionaryAsync(group => group.IdRouteGroup, ct);
            var installationLocalIDs = routeFeedbackDetails.Where(r => r.Key.IdInstallationLocal.IsNotNull())
                                                           .Select(r => r.Key.IdInstallationLocal)
                                                           .Distinct();
            Dictionary<int, (string InstallationLocal, string Description, float Factor)> installationLocalsDictionary =
                new Dictionary<int, (string, string, float)>();
            foreach (var localIDs in installationLocalIDs.GetChunks(500))
            {​
                var locals = await (from local in _context.RBM_InstallationLocal
                                    where localIDs.Contains(local.IdInstallationLocal)
                                    select new
                                    {​
                                        local.IdInstallationLocal,
                                        local.InstallationLocal,
                                        local.Description,
                                        local.IdCriticalityNavigation.Factor
                                    }​).ToListAsync(ct);
                foreach (var local in locals)
                    installationLocalsDictionary.Add(local.IdInstallationLocal, (local.InstallationLocal, local.Description, local.Factor));
            }​
            var checkListItemsIDs = routeFeedbackDetails.Where(r => r.Key.CheckListItemID > 0)
                                                        .Select(r => r.Key.CheckListItemID)
                                                        .Distinct();
            Dictionary<int, (int IdCheckList, string Description, float Factor)> checkListItemsDictionary =
                new Dictionary<int, (int, string, float)>();
            foreach (var itemsIDs in checkListItemsIDs.GetChunks(500))
            {​
                var items = await (from item in _context.AUG_CheckListItems
                                   where itemsIDs.Contains(item.IdCheckListItem)
                                   select new
                                   {​
                                       item.IdCheckListItem,
                                       item.IdCheckList,
                                       item.Description,
                                       item.Factor
                                   }​).ToArrayAsync(ct);
                foreach (var item in items)
                    checkListItemsDictionary.Add(item.IdCheckListItem, (item.IdCheckList, item.Description, item.Factor));
            }​
            var checkListIDs = checkListItemsDictionary.Values
                                                       .Select(r => r.IdCheckList)
                                                       .Distinct();
            var checkLists = new List<IItem>();
            foreach (var ids in checkListIDs.GetChunks(500))
            {​
                var lists = await (from r in _context.AUG_CheckListTemplate
                                   where ids.Contains(r.IdCheckList)
                                   select new Item
                                   {​
                                       ID = r.IdCheckList,
                                       Name = r.CheckList
                                   }​).ToArrayAsync(ct);
                checkLists.AddRange(lists);
            }​
            //var checkListOptionDetailsIDs = routeFeedbackDetails.Where(r => r.CheckListOptionDetailID > 0)
            //                                                    .Select(r => r.CheckListOptionDetailID)
            //                                                    .Distinct();
            var checkListOptionDetailsIDs = new List<int>();
            Dictionary<int, (string CheckListOptionDetail, float Factor)> checkListOptionDetailsDictionary =
                new Dictionary<int, (string, float)>();
            foreach (var optionIDs in checkListOptionDetailsIDs.GetChunks(500))
            {​
                var options = await (from option in _context.AUG_CheckListOptionDetail
                                     where optionIDs.Contains(option.IdCheckListOptionDetail)
                                     select new
                                     {​
                                         option.IdCheckListOptionDetail,
                                         option.CheckListOptionDetail,
                                         option.Factor
                                     }​).ToArrayAsync(ct);
                foreach (var option in options)
                    checkListOptionDetailsDictionary.Add(option.IdCheckListOptionDetail, (option.CheckListOptionDetail, option.Factor));
            }​
            var attachmentsLookup = attachments.ToLookup(attachment => attachment.RouteFeedbackDetailID);
            var commentsLookup = comments.ToLookup(comment => comment.RouteFeedbackDetailID);
            var checkListsDictionary = checkLists.ToDictionary(checklist => checklist.ID);
            return (from item in routeFeedbackDetails
                    let lastItem = item.OrderByDescending(d => d.Timestamp).FirstOrDefault()
                    //let @group = item.RouteGroupID.IsNotNull() ? routeGroups[item.RouteGroupID.Value] : null
                    let local = installationLocalsDictionary[lastItem.IdInstallationLocal]
                    let checkListItem = lastItem.CheckListItemID != null ? checkListItemsDictionary[lastItem.CheckListItemID.Value] : default
                    let checkList = checkListItem.IdCheckList > 0 ? checkListsDictionary[checkListItem.IdCheckList] : default
                    let option = lastItem.CheckListOptionDetailID.IsNotNull() ? checkListOptionDetailsDictionary[lastItem.CheckListOptionDetailID.Value] : default
                    let atta = attachmentsLookup[lastItem.RouteFeedbackDetailID]
                    let comm = commentsLookup[lastItem.RouteFeedbackDetailID]
                    select new PublicCommissioningDetailsDTO
                    {​
                        RouteFeedbackDetailID = lastItem.RouteFeedbackDetailID,
                        RouteGroupID = lastItem.RouteGroupID ?? 0,
                        //RouteGroup = @group?.RouteGroup,
                        //RouteGroupDescription = @group?.Description,
                        RouteID = lastItem.RouteID,
                        Route = lastItem.Route,
                        RouteDescription = lastItem.RouteDescription,
                        InstallationLocal = local.InstallationLocal,
                        Description = local.Description,
                        CheckList = checkList.Name,
                        CheckListItemID = lastItem.CheckListItemID ?? 0,
                        CheckListOptionDetailID = lastItem.CheckListOptionDetailID ?? 0,
                        CheckListOptionDetail = option.CheckListOptionDetail,
                        CheckListItem = checkListItem.Description,
                        EditedBy = lastItem.EditedBy,
                        EditedByName = lastItem.EditedByName,
                        Timestamp = lastItem.Timestamp,
                        PercentComplete = checkListItem.Factor * option.Factor * local.Factor,
                        Attachments = atta.ToArray(),
                        Comments = comm.ToArray()
                    }​).ToArray();
        }​
    
    
  
  

