using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using OrchardCore.AuditTrail.Models;
using OrchardCore.AuditTrail.Permissions;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.AuditTrail.ViewModels;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Navigation;
using OrchardCore.Settings;

namespace OrchardCore.AuditTrail.Controllers
{
    public class AdminController : Controller
    {
        private readonly ISiteService _siteService;
        private readonly IShapeFactory _shapeFactory;
        private readonly IAuditTrailManager _auditTrailManager;
        private readonly IUpdateModelAccessor _updateModelAccessor;
        private readonly IAuthorizationService _authorizationService;
        private readonly IAuditTrailEventDisplayManager _auditTrailEventDisplayManager;

        public AdminController(
            ISiteService siteService,
            IShapeFactory shapeFactory,
            IAuditTrailManager auditTrailManager,
            IUpdateModelAccessor updateModelAccessor,
            IAuthorizationService authorizationService,
            IAuditTrailEventDisplayManager auditTrailEventDisplayManager)
        {
            _siteService = siteService;
            _shapeFactory = shapeFactory;
            _auditTrailManager = auditTrailManager;
            _updateModelAccessor = updateModelAccessor;
            _authorizationService = authorizationService;
            _auditTrailEventDisplayManager = auditTrailEventDisplayManager;
        }

        public async Task<ActionResult> Index(PagerParameters pagerParameters, AuditTrailOrderBy? orderBy = null)
        {
            if (!await _authorizationService.AuthorizeAsync(User, AuditTrailPermissions.ViewAuditTrail))
            {
                return Forbid();
            }

            var siteSettings = await _siteService.GetSiteSettingsAsync();
            var pager = new Pager(pagerParameters, siteSettings.PageSize);
            var filters = Filters.From(QueryHelpers.ParseQuery(Request.QueryString.Value), _updateModelAccessor.ModelUpdater);
            var searchResult =
                await _auditTrailManager.GetAuditTrailEventsAsync(pager.Page, pager.PageSize, filters, orderBy ?? AuditTrailOrderBy.DateDescending);

            if (!_updateModelAccessor.ModelUpdater.ModelState.IsValid)
            {
                searchResult.AuditTrailEvents = Enumerable.Empty<AuditTrailEvent>();
            }

            var pagerShape = (await _shapeFactory.New.Pager(pager)).TotalItemCount(searchResult.TotalCount);

            var eventDescriptors = _auditTrailManager.DescribeCategories()
                .SelectMany(categoryDescriptor => categoryDescriptor.Events)
                .ToDictionary(eventDescriptor => eventDescriptor.FullEventName);

            var auditTrailEventsSummaryViewModel = searchResult.AuditTrailEvents.Select(auditTrailEvent =>
            {
                var eventDescriptor = eventDescriptors.ContainsKey(auditTrailEvent.FullEventName)
                    ? eventDescriptors[auditTrailEvent.FullEventName]
                    : AuditTrailEventDescriptor.Basic(auditTrailEvent);

                return new AuditTrailEventSummaryViewModel
                {
                    AuditTrailEvent = auditTrailEvent,
                    EventDescriptor = eventDescriptor,
                    CategoryDescriptor = eventDescriptor.CategoryDescriptor,
                };
            }).ToArray();

            foreach (var model in auditTrailEventsSummaryViewModel)
            {
                model.AdditionalColumnsShapes = await _auditTrailEventDisplayManager.BuildAdditionalColumnsShapesAsync(model.AuditTrailEvent);
                model.SummaryShape = await _auditTrailEventDisplayManager.BuildDisplayAsync(model.AuditTrailEvent, "SummaryAdmin");
                model.ActionsShape = await _auditTrailEventDisplayManager.BuildActionsAsync(model.AuditTrailEvent, "SummaryAdmin");
            }

            return View(new AuditTrailViewModel
            {
                AuditTrailEvents = auditTrailEventsSummaryViewModel,
                AdditionalColumnNames = await _auditTrailEventDisplayManager.BuildAdditionalColumnNamesShapesAsync(),
                FilterDisplay = await _auditTrailEventDisplayManager.BuildFilterDisplayAsync(filters),
                OrderBy = orderBy ?? AuditTrailOrderBy.DateDescending,
                Pager = pagerShape
            });
        }

        public async Task<ActionResult> Detail(string auditTrailEventId)
        {
            if (!await _authorizationService.AuthorizeAsync(User, AuditTrailPermissions.ViewAuditTrail))
            {
                return Forbid();
            }

            var auditTrailEvent = await _auditTrailManager.GetAuditTrailEventAsync(auditTrailEventId);

            return View(new AuditTrailDetailsViewModel
            {
                AuditTrailEvent = auditTrailEvent,
                Descriptor = _auditTrailManager.DescribeEvent(auditTrailEvent),
                DetailsShape = await _auditTrailEventDisplayManager.BuildDisplayAsync(auditTrailEvent, "Detail")
            });
        }
    }
}