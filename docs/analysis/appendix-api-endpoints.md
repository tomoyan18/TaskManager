# 付録: Plane API エンドポイント一覧（機械抽出）

> **注意**: これは Plane（参照用リポジトリ）の URL 定義を機械抽出した解析資料である。TaskManager の API 名・ルートとして流用してはならない（CLAUDE.md §10）。TaskManager の API は Phase 2 で独立設計する。

- 抽出元: `apps/api/plane/{app,api,space}/urls/*.py`, `apps/api/plane/authentication/urls.py`
- 抽出方法: `path("...", View.as_view({...}), name=...)` を正規表現で抽出（スクラッチパッド上の一時スクリプト。TaskManager には含めない）
- 表記: `W` = `workspaces/<str:slug>`、`P` = `projects/<uuid:project_id>`。`(APIView)` はクラス内で定義された get/post/patch/delete 等をメソッド単位で持つ（個別メソッドは各ファイルで確認が必要）
- 件数: 内部 API(/api/)      233 件、公開 API(/api/v1/)       73 件、公開ボード(/api/public/)       25 件、認証(/auth/)       37 件

## 内部 API（Web アプリ用・セッション認証）

| 定義ファイル | ルート | View | HTTP メソッド → アクション |
|---|---|---|---|
| analytic.py | `/api/W/analytics/` | AnalyticsEndpoint | (APIView) |
| analytic.py | `/api/W/analytic-view/` | AnalyticViewViewset | GET:list POST:create |
| analytic.py | `/api/W/analytic-view/<uuid:pk>/` | AnalyticViewViewset | GET:retrieve PATCH:partial_update DELETE:destroy |
| analytic.py | `/api/W/saved-analytic-view/<uuid:analytic_id>/` | SavedAnalyticEndpoint | (APIView) |
| analytic.py | `/api/W/export-analytics/` | ExportAnalyticsEndpoint | (APIView) |
| analytic.py | `/api/W/default-analytics/` | DefaultAnalyticsEndpoint | (APIView) |
| analytic.py | `/api/W/project-stats/` | ProjectStatsEndpoint | (APIView) |
| analytic.py | `/api/W/advance-analytics/` | AdvanceAnalyticsEndpoint | (APIView) |
| analytic.py | `/api/W/advance-analytics-stats/` | AdvanceAnalyticsStatsEndpoint | (APIView) |
| analytic.py | `/api/W/advance-analytics-charts/` | AdvanceAnalyticsChartEndpoint | (APIView) |
| analytic.py | `/api/W/P/advance-analytics/` | ProjectAdvanceAnalyticsEndpoint | (APIView) |
| analytic.py | `/api/W/P/advance-analytics-stats/` | ProjectAdvanceAnalyticsStatsEndpoint | (APIView) |
| analytic.py | `/api/W/P/advance-analytics-charts/` | ProjectAdvanceAnalyticsChartEndpoint | (APIView) |
| api.py | `/api/users/api-tokens/` | ApiTokenEndpoint | (APIView) |
| api.py | `/api/users/api-tokens/<uuid:pk>/` | ApiTokenEndpoint | (APIView) |
| asset.py | `/api/W/file-assets/` | FileAssetEndpoint | (APIView) |
| asset.py | `/api/workspaces/file-assets/<uuid:workspace_id>/<str:asset_key>/` | FileAssetEndpoint | (APIView) |
| asset.py | `/api/users/file-assets/` | UserAssetsEndpoint | (APIView) |
| asset.py | `/api/users/file-assets/<str:asset_key>/` | UserAssetsEndpoint | (APIView) |
| asset.py | `/api/workspaces/file-assets/<uuid:workspace_id>/<str:asset_key>/restore/` | FileAssetViewSet | POST:restore |
| asset.py | `/api/assets/v2/W/` | WorkspaceFileAssetEndpoint | (APIView) |
| asset.py | `/api/assets/v2/W/<uuid:asset_id>/` | WorkspaceFileAssetEndpoint | (APIView) |
| asset.py | `/api/assets/v2/user-assets/` | UserAssetsV2Endpoint | (APIView) |
| asset.py | `/api/assets/v2/user-assets/<uuid:asset_id>/` | UserAssetsV2Endpoint | (APIView) |
| asset.py | `/api/assets/v2/W/restore/<uuid:asset_id>/` | AssetRestoreEndpoint | (APIView) |
| asset.py | `/api/assets/v2/static/<uuid:asset_id>/` | StaticFileAssetEndpoint | (APIView) |
| asset.py | `/api/assets/v2/W/P/` | ProjectAssetEndpoint | (APIView) |
| asset.py | `/api/assets/v2/W/P/<uuid:pk>/` | ProjectAssetEndpoint | (APIView) |
| asset.py | `/api/assets/v2/W/P/<uuid:entity_id>/bulk/` | ProjectBulkAssetEndpoint | (APIView) |
| asset.py | `/api/assets/v2/W/check/<uuid:asset_id>/` | AssetCheckEndpoint | (APIView) |
| asset.py | `/api/assets/v2/W/duplicate-assets/<uuid:asset_id>/` | DuplicateAssetEndpoint | (APIView) |
| asset.py | `/api/assets/v2/W/download/<uuid:asset_id>/` | WorkspaceAssetDownloadEndpoint | (APIView) |
| asset.py | `/api/assets/v2/W/P/download/<uuid:asset_id>/` | ProjectAssetDownloadEndpoint | (APIView) |
| cycle.py | `/api/W/P/cycles/` | CycleViewSet | GET:list POST:create |
| cycle.py | `/api/W/P/cycles/<uuid:pk>/` | CycleViewSet | GET:retrieve PUT:update PATCH:partial_update DELETE:destroy |
| cycle.py | `/api/W/P/cycles/<uuid:cycle_id>/cycle-issues/` | CycleIssueViewSet | GET:list POST:create |
| cycle.py | `/api/W/P/cycles/<uuid:cycle_id>/cycle-issues/<uuid:issue_id>/` | CycleIssueViewSet | GET:retrieve PUT:update PATCH:partial_update DELETE:destroy |
| cycle.py | `/api/W/P/cycles/date-check/` | CycleDateCheckEndpoint | (APIView) |
| cycle.py | `/api/W/P/user-favorite-cycles/` | CycleFavoriteViewSet | GET:list POST:create |
| cycle.py | `/api/W/P/user-favorite-cycles/<uuid:cycle_id>/` | CycleFavoriteViewSet | DELETE:destroy |
| cycle.py | `/api/W/P/cycles/<uuid:cycle_id>/transfer-issues/` | TransferCycleIssueEndpoint | (APIView) |
| cycle.py | `/api/W/P/cycles/<uuid:cycle_id>/user-properties/` | CycleUserPropertiesEndpoint | (APIView) |
| cycle.py | `/api/W/P/cycles/<uuid:cycle_id>/archive/` | CycleArchiveUnarchiveEndpoint | (APIView) |
| cycle.py | `/api/W/P/archived-cycles/` | CycleArchiveUnarchiveEndpoint | (APIView) |
| cycle.py | `/api/W/P/archived-cycles/<uuid:pk>/` | CycleArchiveUnarchiveEndpoint | (APIView) |
| cycle.py | `/api/W/P/cycles/<uuid:cycle_id>/progress/` | CycleProgressEndpoint | (APIView) |
| cycle.py | `/api/W/P/cycles/<uuid:cycle_id>/analytics/` | CycleAnalyticsEndpoint | (APIView) |
| estimate.py | `/api/W/P/project-estimates/` | ProjectEstimatePointEndpoint | (APIView) |
| estimate.py | `/api/W/P/estimates/` | BulkEstimatePointEndpoint | GET:list POST:create |
| estimate.py | `/api/W/P/estimates/<uuid:estimate_id>/` | BulkEstimatePointEndpoint | GET:retrieve PATCH:partial_update DELETE:destroy |
| estimate.py | `/api/W/P/estimates/<uuid:estimate_id>/estimate-points/` | EstimatePointEndpoint | POST:create |
| estimate.py | `/api/W/P/estimates/<uuid:estimate_id>/estimate-points/<estimate_point_id>/` | EstimatePointEndpoint | PATCH:partial_update DELETE:destroy |
| exporter.py | `/api/W/export-issues/` | ExportIssuesEndpoint | (APIView) |
| external.py | `/api/unsplash/` | UnsplashEndpoint | (APIView) |
| external.py | `/api/W/P/ai-assistant/` | GPTIntegrationEndpoint | (APIView) |
| external.py | `/api/W/ai-assistant/` | WorkspaceGPTIntegrationEndpoint | (APIView) |
| intake.py | `/api/W/P/intakes/` | IntakeViewSet | GET:list POST:create |
| intake.py | `/api/W/P/intakes/<uuid:pk>/` | IntakeViewSet | GET:retrieve PATCH:partial_update DELETE:destroy |
| intake.py | `/api/W/P/intake-issues/` | IntakeIssueViewSet | GET:list POST:create |
| intake.py | `/api/W/P/intake-issues/<uuid:pk>/` | IntakeIssueViewSet | GET:retrieve PATCH:partial_update DELETE:destroy |
| intake.py | `/api/W/P/inboxes/` | IntakeViewSet | GET:list POST:create |
| intake.py | `/api/W/P/inboxes/<uuid:pk>/` | IntakeViewSet | GET:retrieve PATCH:partial_update DELETE:destroy |
| intake.py | `/api/W/P/inbox-issues/` | IntakeIssueViewSet | GET:list POST:create |
| intake.py | `/api/W/P/inbox-issues/<uuid:pk>/` | IntakeIssueViewSet | GET:retrieve PATCH:partial_update DELETE:destroy |
| intake.py | `/api/W/P/intake-work-items/<uuid:work_item_id>/description-versions/` | IntakeWorkItemDescriptionVersionEndpoint | (APIView) |
| intake.py | `/api/W/P/intake-work-items/<uuid:work_item_id>/description-versions/<uuid:pk>/` | IntakeWorkItemDescriptionVersionEndpoint | (APIView) |
| issue.py | `/api/W/P/issues/list/` | IssueListEndpoint | (APIView) |
| issue.py | `/api/W/P/issues/` | IssueViewSet | GET:list POST:create |
| issue.py | `/api/W/P/issues-detail/` | IssueDetailEndpoint | (APIView) |
| issue.py | `/api/W/P/v2/issues/` | IssuePaginatedViewSet | GET:list |
| issue.py | `/api/W/P/issues/<uuid:pk>/` | IssueViewSet | GET:retrieve PUT:update PATCH:partial_update DELETE:destroy |
| issue.py | `/api/W/P/issue-labels/` | LabelViewSet | GET:list POST:create |
| issue.py | `/api/W/P/issue-labels/<uuid:pk>/` | LabelViewSet | GET:retrieve PUT:update PATCH:partial_update DELETE:destroy |
| issue.py | `/api/W/P/bulk-create-labels/` | BulkCreateIssueLabelsEndpoint | (APIView) |
| issue.py | `/api/W/P/bulk-delete-issues/` | BulkDeleteIssuesEndpoint | (APIView) |
| issue.py | `/api/W/P/bulk-archive-issues/` | BulkArchiveIssuesEndpoint | (APIView) |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/sub-issues/` | SubIssuesEndpoint | (APIView) |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/issue-links/` | IssueLinkViewSet | GET:list POST:create |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/issue-links/<uuid:pk>/` | IssueLinkViewSet | GET:retrieve PUT:update PATCH:partial_update DELETE:destroy |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/issue-attachments/` | IssueAttachmentEndpoint | (APIView) |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/issue-attachments/<uuid:pk>/` | IssueAttachmentEndpoint | (APIView) |
| issue.py | `/api/assets/v2/W/P/issues/<uuid:issue_id>/attachments/` | IssueAttachmentV2Endpoint | (APIView) |
| issue.py | `/api/assets/v2/W/P/issues/<uuid:issue_id>/attachments/<uuid:pk>/` | IssueAttachmentV2Endpoint | (APIView) |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/history/` | IssueActivityEndpoint | (APIView) |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/comments/` | IssueCommentViewSet | GET:list POST:create |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/comments/<uuid:pk>/` | IssueCommentViewSet | GET:retrieve PUT:update PATCH:partial_update DELETE:destroy |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/issue-subscribers/` | IssueSubscriberViewSet | GET:list POST:create |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/issue-subscribers/<uuid:subscriber_id>/` | IssueSubscriberViewSet | DELETE:destroy |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/subscribe/` | IssueSubscriberViewSet | GET:subscription_status POST:subscribe DELETE:unsubscribe |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/reactions/` | IssueReactionViewSet | GET:list POST:create |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/reactions/<str:reaction_code>/` | IssueReactionViewSet | DELETE:destroy |
| issue.py | `/api/W/P/comments/<uuid:comment_id>/reactions/` | CommentReactionViewSet | GET:list POST:create |
| issue.py | `/api/W/P/comments/<uuid:comment_id>/reactions/<str:reaction_code>/` | CommentReactionViewSet | DELETE:destroy |
| issue.py | `/api/W/P/user-properties/` | ProjectUserDisplayPropertyEndpoint | (APIView) |
| issue.py | `/api/W/P/archived-issues/` | IssueArchiveViewSet | GET:list |
| issue.py | `/api/W/P/issues/<uuid:pk>/archive/` | IssueArchiveViewSet | GET:retrieve POST:archive DELETE:unarchive |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/issue-relation/` | IssueRelationViewSet | GET:list POST:create |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/remove-relation/` | IssueRelationViewSet | POST:remove_relation |
| issue.py | `/api/W/P/deleted-issues/` | DeletedIssuesListViewSet | (APIView) |
| issue.py | `/api/W/P/issue-dates/` | IssueBulkUpdateDateEndpoint | (APIView) |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/versions/` | IssueVersionEndpoint | (APIView) |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/versions/<uuid:pk>/` | IssueVersionEndpoint | (APIView) |
| issue.py | `/api/W/P/work-items/<uuid:work_item_id>/description-versions/` | WorkItemDescriptionVersionEndpoint | (APIView) |
| issue.py | `/api/W/P/work-items/<uuid:work_item_id>/description-versions/<uuid:pk>/` | WorkItemDescriptionVersionEndpoint | (APIView) |
| issue.py | `/api/W/P/issues/<uuid:issue_id>/meta/` | IssueMetaEndpoint | (APIView) |
| issue.py | `/api/W/work-items/<str:project_identifier>-<str:issue_identifier>/` | IssueDetailIdentifierEndpoint | (APIView) |
| module.py | `/api/W/P/modules/` | ModuleViewSet | GET:list POST:create |
| module.py | `/api/W/P/modules/<uuid:pk>/` | ModuleViewSet | GET:retrieve PUT:update PATCH:partial_update DELETE:destroy |
| module.py | `/api/W/P/issues/<uuid:issue_id>/modules/` | ModuleIssueViewSet | POST:create_issue_modules |
| module.py | `/api/W/P/modules/<uuid:module_id>/issues/` | ModuleIssueViewSet | POST:create_module_issues GET:list |
| module.py | `/api/W/P/modules/<uuid:module_id>/issues/<uuid:issue_id>/` | ModuleIssueViewSet | GET:retrieve PUT:update PATCH:partial_update DELETE:destroy |
| module.py | `/api/W/P/modules/<uuid:module_id>/module-links/` | ModuleLinkViewSet | GET:list POST:create |
| module.py | `/api/W/P/modules/<uuid:module_id>/module-links/<uuid:pk>/` | ModuleLinkViewSet | GET:retrieve PUT:update PATCH:partial_update DELETE:destroy |
| module.py | `/api/W/P/user-favorite-modules/` | ModuleFavoriteViewSet | GET:list POST:create |
| module.py | `/api/W/P/user-favorite-modules/<uuid:module_id>/` | ModuleFavoriteViewSet | DELETE:destroy |
| module.py | `/api/W/P/modules/<uuid:module_id>/user-properties/` | ModuleUserPropertiesEndpoint | (APIView) |
| module.py | `/api/W/P/modules/<uuid:module_id>/archive/` | ModuleArchiveUnarchiveEndpoint | (APIView) |
| module.py | `/api/W/P/archived-modules/` | ModuleArchiveUnarchiveEndpoint | (APIView) |
| module.py | `/api/W/P/archived-modules/<uuid:pk>/` | ModuleArchiveUnarchiveEndpoint | (APIView) |
| notification.py | `/api/W/users/notifications/` | NotificationViewSet | GET:list |
| notification.py | `/api/W/users/notifications/<uuid:pk>/` | NotificationViewSet | GET:retrieve PATCH:partial_update DELETE:destroy |
| notification.py | `/api/W/users/notifications/<uuid:pk>/read/` | NotificationViewSet | POST:mark_read DELETE:mark_unread |
| notification.py | `/api/W/users/notifications/<uuid:pk>/archive/` | NotificationViewSet | POST:archive DELETE:unarchive |
| notification.py | `/api/W/users/notifications/unread/` | UnreadNotificationEndpoint | (APIView) |
| notification.py | `/api/W/users/notifications/mark-all-read/` | MarkAllReadNotificationViewSet | POST:create |
| notification.py | `/api/users/me/notification-preferences/` | UserNotificationPreferenceEndpoint | (APIView) |
| page.py | `/api/W/P/pages-summary/` | PageViewSet | GET:summary |
| page.py | `/api/W/P/pages/` | PageViewSet | GET:list POST:create |
| page.py | `/api/W/P/pages/<uuid:page_id>/` | PageViewSet | GET:retrieve PATCH:partial_update DELETE:destroy |
| page.py | `/api/W/P/favorite-pages/<uuid:page_id>/` | PageFavoriteViewSet | POST:create DELETE:destroy |
| page.py | `/api/W/P/pages/<uuid:page_id>/archive/` | PageViewSet | POST:archive DELETE:unarchive |
| page.py | `/api/W/P/pages/<uuid:page_id>/lock/` | PageViewSet | POST:lock DELETE:unlock |
| page.py | `/api/W/P/pages/<uuid:page_id>/access/` | PageViewSet | POST:access |
| page.py | `/api/W/P/pages/<uuid:page_id>/description/` | PagesDescriptionViewSet | GET:retrieve PATCH:partial_update |
| page.py | `/api/W/P/pages/<uuid:page_id>/versions/` | PageVersionEndpoint | (APIView) |
| page.py | `/api/W/P/pages/<uuid:page_id>/versions/<uuid:pk>/` | PageVersionEndpoint | (APIView) |
| page.py | `/api/W/P/pages/<uuid:page_id>/duplicate/` | PageDuplicateEndpoint | (APIView) |
| project.py | `/api/W/projects/` | ProjectViewSet | GET:list POST:create |
| project.py | `/api/W/projects/details/` | ProjectViewSet | GET:list_detail |
| project.py | `/api/W/projects/<uuid:pk>/` | ProjectViewSet | GET:retrieve PUT:update PATCH:partial_update DELETE:destroy |
| project.py | `/api/W/project-identifiers/` | ProjectIdentifierEndpoint | (APIView) |
| project.py | `/api/W/P/invitations/` | ProjectInvitationsViewset | GET:list POST:create |
| project.py | `/api/W/P/invitations/<uuid:pk>/` | ProjectInvitationsViewset | GET:retrieve DELETE:destroy |
| project.py | `/api/users/me/W/projects/invitations/` | UserProjectInvitationsViewset | GET:list POST:create |
| project.py | `/api/users/me/W/project-roles/` | UserProjectRolesEndpoint | (APIView) |
| project.py | `/api/W/P/join/<uuid:pk>/` | ProjectJoinEndpoint | (APIView) |
| project.py | `/api/W/P/members/` | ProjectMemberViewSet | GET:list POST:create |
| project.py | `/api/W/P/members/<uuid:pk>/` | ProjectMemberViewSet | GET:retrieve PATCH:partial_update DELETE:destroy |
| project.py | `/api/W/P/members/leave/` | ProjectMemberViewSet | POST:leave |
| project.py | `/api/W/P/project-views/` | ProjectUserViewsEndpoint | (APIView) |
| project.py | `/api/W/P/project-members/me/` | ProjectMemberUserEndpoint | (APIView) |
| project.py | `/api/W/user-favorite-projects/` | ProjectFavoritesViewSet | GET:list POST:create |
| project.py | `/api/W/user-favorite-P/` | ProjectFavoritesViewSet | DELETE:destroy |
| project.py | `/api/W/P/project-deploy-boards/` | DeployBoardViewSet | GET:list POST:create |
| project.py | `/api/W/P/project-deploy-boards/<uuid:pk>/` | DeployBoardViewSet | GET:retrieve PATCH:partial_update DELETE:destroy |
| project.py | `/api/W/P/archive/` | ProjectArchiveUnarchiveEndpoint | (APIView) |
| project.py | `/api/W/P/preferences/member/<uuid:member_id>/` | ProjectMemberPreferenceEndpoint | (APIView) |
| search.py | `/api/W/search/` | GlobalSearchEndpoint | (APIView) |
| search.py | `/api/W/P/search-issues/` | IssueSearchEndpoint | (APIView) |
| search.py | `/api/W/entity-search/` | SearchEndpoint | (APIView) |
| state.py | `/api/W/P/states/` | StateViewSet | GET:list POST:create |
| state.py | `/api/W/P/states/<uuid:pk>/` | StateViewSet | GET:retrieve PATCH:partial_update DELETE:destroy |
| state.py | `/api/W/P/intake-state/` | IntakeStateEndpoint | (APIView) |
| state.py | `/api/W/P/states/<uuid:pk>/mark-default/` | StateViewSet | POST:mark_as_default |
| timezone.py | `/api/timezones/` | TimezoneEndpoint | (APIView) |
| user.py | `/api/users/me/` | UserEndpoint | GET:retrieve PATCH:partial_update DELETE:deactivate |
| user.py | `/api/users/session/` | UserSessionEndpoint | (APIView) |
| user.py | `/api/users/me/settings/` | UserEndpoint | GET:retrieve_user_settings |
| user.py | `/api/users/me/email/generate-code/` | UserEndpoint | POST:generate_email_verification_code |
| user.py | `/api/users/me/email/` | UserEndpoint | PATCH:update_email |
| user.py | `/api/users/me/profile/` | ProfileEndpoint | (APIView) |
| user.py | `/api/users/me/accounts/` | AccountEndpoint | (APIView) |
| user.py | `/api/users/me/accounts/<uuid:pk>/` | AccountEndpoint | (APIView) |
| user.py | `/api/users/me/instance-admin/` | UserEndpoint | GET:retrieve_instance_admin |
| user.py | `/api/users/me/onboard/` | UpdateUserOnBoardedEndpoint | (APIView) |
| user.py | `/api/users/me/tour-completed/` | UpdateUserTourCompletedEndpoint | (APIView) |
| user.py | `/api/users/me/activities/` | UserActivityEndpoint | (APIView) |
| user.py | `/api/users/me/workspaces/` | UserWorkSpacesEndpoint | (APIView) |
| user.py | `/api/users/me/W/activity-graph/` | UserActivityGraphEndpoint | (APIView) |
| user.py | `/api/users/me/W/issues-completed-graph/` | UserIssueCompletedGraphEndpoint | (APIView) |
| user.py | `/api/users/me/W/dashboard/` | UserWorkspaceDashboardEndpoint | (APIView) |
| views.py | `/api/W/P/views/` | IssueViewViewSet | GET:list POST:create |
| views.py | `/api/W/P/views/<uuid:pk>/` | IssueViewViewSet | GET:retrieve PUT:update PATCH:partial_update DELETE:destroy |
| views.py | `/api/W/views/` | WorkspaceViewViewSet | GET:list POST:create |
| views.py | `/api/W/views/<uuid:pk>/` | WorkspaceViewViewSet | GET:retrieve PUT:update PATCH:partial_update DELETE:destroy |
| views.py | `/api/W/issues/` | WorkspaceViewIssuesViewSet | GET:list |
| views.py | `/api/W/P/user-favorite-views/` | IssueViewFavoriteViewSet | GET:list POST:create |
| views.py | `/api/W/P/user-favorite-views/<uuid:view_id>/` | IssueViewFavoriteViewSet | DELETE:destroy |
| webhook.py | `/api/W/webhooks/` | WebhookEndpoint | (APIView) |
| webhook.py | `/api/W/webhooks/<uuid:pk>/` | WebhookEndpoint | (APIView) |
| webhook.py | `/api/W/webhooks/<uuid:pk>/regenerate/` | WebhookSecretRegenerateEndpoint | (APIView) |
| webhook.py | `/api/W/webhook-logs/<uuid:webhook_id>/` | WebhookLogsEndpoint | (APIView) |
| workspace.py | `/api/workspace-slug-check/` | WorkSpaceAvailabilityCheckEndpoint | (APIView) |
| workspace.py | `/api/workspaces/` | WorkSpaceViewSet | GET:list POST:create |
| workspace.py | `/api/W/` | WorkSpaceViewSet | GET:retrieve PUT:update PATCH:partial_update DELETE:destroy |
| workspace.py | `/api/W/invitations/` | WorkspaceInvitationsViewset | GET:list POST:create |
| workspace.py | `/api/W/invitations/<uuid:pk>/` | WorkspaceInvitationsViewset | DELETE:destroy GET:retrieve PATCH:partial_update |
| workspace.py | `/api/users/me/workspaces/invitations/` | UserWorkspaceInvitationsViewSet | GET:list POST:create |
| workspace.py | `/api/W/invitations/<uuid:pk>/join/` | WorkspaceJoinEndpoint | (APIView) |
| workspace.py | `/api/W/members/` | WorkSpaceMemberViewSet | GET:list |
| workspace.py | `/api/W/project-members/` | WorkspaceProjectMemberEndpoint | (APIView) |
| workspace.py | `/api/W/members/<uuid:pk>/` | WorkSpaceMemberViewSet | PATCH:partial_update DELETE:destroy GET:retrieve |
| workspace.py | `/api/W/members/leave/` | WorkSpaceMemberViewSet | POST:leave |
| workspace.py | `/api/users/last-visited-workspace/` | UserLastProjectWithWorkspaceEndpoint | (APIView) |
| workspace.py | `/api/W/workspace-members/me/` | WorkspaceMemberUserEndpoint | (APIView) |
| workspace.py | `/api/W/workspace-views/` | WorkspaceMemberUserViewsEndpoint | (APIView) |
| workspace.py | `/api/W/workspace-themes/` | WorkspaceThemeViewSet | GET:list POST:create |
| workspace.py | `/api/W/workspace-themes/<uuid:pk>/` | WorkspaceThemeViewSet | GET:retrieve PATCH:partial_update DELETE:destroy |
| workspace.py | `/api/W/user-stats/<uuid:user_id>/` | WorkspaceUserProfileStatsEndpoint | (APIView) |
| workspace.py | `/api/W/user-activity/<uuid:user_id>/` | WorkspaceUserActivityEndpoint | (APIView) |
| workspace.py | `/api/W/user-activity/<uuid:user_id>/export/` | ExportWorkspaceUserActivityEndpoint | (APIView) |
| workspace.py | `/api/W/user-profile/<uuid:user_id>/` | WorkspaceUserProfileEndpoint | (APIView) |
| workspace.py | `/api/W/user-issues/<uuid:user_id>/` | WorkspaceUserProfileIssuesEndpoint | (APIView) |
| workspace.py | `/api/W/labels/` | WorkspaceLabelsEndpoint | (APIView) |
| workspace.py | `/api/W/user-properties/` | WorkspaceUserPropertiesEndpoint | (APIView) |
| workspace.py | `/api/W/states/` | WorkspaceStatesEndpoint | (APIView) |
| workspace.py | `/api/W/estimates/` | WorkspaceEstimatesEndpoint | (APIView) |
| workspace.py | `/api/W/modules/` | WorkspaceModulesEndpoint | (APIView) |
| workspace.py | `/api/W/cycles/` | WorkspaceCyclesEndpoint | (APIView) |
| workspace.py | `/api/W/user-favorites/` | WorkspaceFavoriteEndpoint | (APIView) |
| workspace.py | `/api/W/user-favorites/<uuid:favorite_id>/` | WorkspaceFavoriteEndpoint | (APIView) |
| workspace.py | `/api/W/user-favorites/<uuid:favorite_id>/group/` | WorkspaceFavoriteGroupEndpoint | (APIView) |
| workspace.py | `/api/W/draft-issues/` | WorkspaceDraftIssueViewSet | GET:list POST:create |
| workspace.py | `/api/W/draft-issues/<uuid:pk>/` | WorkspaceDraftIssueViewSet | GET:retrieve PATCH:partial_update DELETE:destroy |
| workspace.py | `/api/W/draft-to-issue/<uuid:draft_id>/` | WorkspaceDraftIssueViewSet | POST:create_draft_to_issue |
| workspace.py | `/api/W/quick-links/` | QuickLinkViewSet | GET:list POST:create |
| workspace.py | `/api/W/quick-links/<uuid:pk>/` | QuickLinkViewSet | GET:retrieve PATCH:partial_update DELETE:destroy |
| workspace.py | `/api/W/home-preferences/` | WorkspaceHomePreferenceViewSet | (APIView) |
| workspace.py | `/api/W/home-preferences/<str:key>/` | WorkspaceHomePreferenceViewSet | (APIView) |
| workspace.py | `/api/W/recent-visits/` | UserRecentVisitViewSet | GET:list |
| workspace.py | `/api/W/stickies/` | WorkspaceStickyViewSet | GET:list POST:create |
| workspace.py | `/api/W/stickies/<uuid:pk>/` | WorkspaceStickyViewSet | GET:retrieve PATCH:partial_update DELETE:destroy |
| workspace.py | `/api/W/sidebar-preferences/` | WorkspaceUserPreferenceViewSet | (APIView) |

## 公開 API（API Key 認証・OpenAPI 対象）

| 定義ファイル | ルート | View | HTTP メソッド → アクション |
|---|---|---|---|
| asset.py | `/api/v1/assets/user-assets/` | UserAssetEndpoint | (APIView) |
| asset.py | `/api/v1/assets/user-assets/<uuid:asset_id>/` | UserAssetEndpoint | (APIView) |
| asset.py | `/api/v1/assets/user-assets/server/` | UserServerAssetEndpoint | (APIView) |
| asset.py | `/api/v1/assets/user-assets/<uuid:asset_id>/server/` | UserServerAssetEndpoint | (APIView) |
| asset.py | `/api/v1/W/assets/` | GenericAssetEndpoint | (APIView) |
| asset.py | `/api/v1/W/assets/<uuid:asset_id>/` | GenericAssetEndpoint | (APIView) |
| cycle.py | `/api/v1/W/P/cycles/` | CycleListCreateAPIEndpoint | (APIView) |
| cycle.py | `/api/v1/W/P/cycles-lite/` | CycleListLiteAPIEndpoint | (APIView) |
| cycle.py | `/api/v1/W/P/cycles/<uuid:pk>/` | CycleDetailAPIEndpoint | (APIView) |
| cycle.py | `/api/v1/W/P/cycles/<uuid:cycle_id>/cycle-issues/` | CycleIssueListCreateAPIEndpoint | (APIView) |
| cycle.py | `/api/v1/W/P/cycles/<uuid:cycle_id>/cycle-issues/<uuid:issue_id>/` | CycleIssueDetailAPIEndpoint | (APIView) |
| cycle.py | `/api/v1/W/P/cycles/<uuid:cycle_id>/transfer-issues/` | TransferCycleIssueAPIEndpoint | (APIView) |
| cycle.py | `/api/v1/W/P/cycles/<uuid:cycle_id>/archive/` | CycleArchiveUnarchiveAPIEndpoint | (APIView) |
| cycle.py | `/api/v1/W/P/archived-cycles/` | CycleArchiveUnarchiveAPIEndpoint | (APIView) |
| cycle.py | `/api/v1/W/P/archived-cycles/<uuid:cycle_id>/unarchive/` | CycleArchiveUnarchiveAPIEndpoint | (APIView) |
| estimate.py | `/api/v1/W/P/estimates/` | ProjectEstimateAPIEndpoint | (APIView) |
| estimate.py | `/api/v1/W/P/estimates/<uuid:estimate_id>/estimate-points/` | EstimatePointListCreateAPIEndpoint | (APIView) |
| estimate.py | `/api/v1/W/P/estimates/<uuid:estimate_id>/estimate-points/<uuid:estimate_point_id>/` | EstimatePointDetailAPIEndpoint | (APIView) |
| intake.py | `/api/v1/W/P/intake-issues/` | IntakeIssueListCreateAPIEndpoint | (APIView) |
| intake.py | `/api/v1/W/P/intake-issues/<uuid:issue_id>/` | IntakeIssueDetailAPIEndpoint | (APIView) |
| label.py | `/api/v1/W/P/labels/` | LabelListCreateAPIEndpoint | (APIView) |
| label.py | `/api/v1/W/P/labels/<uuid:pk>/` | LabelDetailAPIEndpoint | (APIView) |
| member.py | `/api/v1/W/P/members/` | ProjectMemberListCreateAPIEndpoint | (APIView) |
| member.py | `/api/v1/W/P/members/<uuid:pk>/` | ProjectMemberDetailAPIEndpoint | (APIView) |
| member.py | `/api/v1/W/P/project-members/` | ProjectMemberListCreateAPIEndpoint | (APIView) |
| member.py | `/api/v1/W/P/project-members-lite/` | ProjectMemberLiteAPIEndpoint | (APIView) |
| member.py | `/api/v1/W/P/project-members/<uuid:pk>/` | ProjectMemberDetailAPIEndpoint | (APIView) |
| member.py | `/api/v1/W/members/` | WorkspaceMemberAPIEndpoint | (APIView) |
| member.py | `/api/v1/W/members-lite/` | WorkspaceMemberLiteAPIEndpoint | (APIView) |
| module.py | `/api/v1/W/P/modules/` | ModuleListCreateAPIEndpoint | (APIView) |
| module.py | `/api/v1/W/P/modules-lite/` | ModuleListLiteAPIEndpoint | (APIView) |
| module.py | `/api/v1/W/P/modules/<uuid:pk>/` | ModuleDetailAPIEndpoint | (APIView) |
| module.py | `/api/v1/W/P/modules/<uuid:module_id>/module-issues/` | ModuleIssueListCreateAPIEndpoint | (APIView) |
| module.py | `/api/v1/W/P/modules/<uuid:module_id>/module-issues/<uuid:issue_id>/` | ModuleIssueDetailAPIEndpoint | (APIView) |
| module.py | `/api/v1/W/P/modules/<uuid:pk>/archive/` | ModuleArchiveUnarchiveAPIEndpoint | (APIView) |
| module.py | `/api/v1/W/P/archived-modules/` | ModuleArchiveUnarchiveAPIEndpoint | (APIView) |
| module.py | `/api/v1/W/P/archived-modules/<uuid:pk>/unarchive/` | ModuleArchiveUnarchiveAPIEndpoint | (APIView) |
| project.py | `/api/v1/W/projects/` | ProjectListCreateAPIEndpoint | (APIView) |
| project.py | `/api/v1/W/projects-lite/` | ProjectListLiteAPIEndpoint | (APIView) |
| project.py | `/api/v1/W/projects/<uuid:pk>/` | ProjectDetailAPIEndpoint | (APIView) |
| project.py | `/api/v1/W/P/archive/` | ProjectArchiveUnarchiveAPIEndpoint | (APIView) |
| project.py | `/api/v1/W/P/summary/` | ProjectSummaryAPIEndpoint | (APIView) |
| schema.py | `/api/v1/schema/` | SpectacularAPIView | (APIView) |
| schema.py | `/api/v1/schema/swagger-ui/` | SpectacularSwaggerView | (APIView) |
| schema.py | `/api/v1/schema/redoc/` | SpectacularRedocView | (APIView) |
| state.py | `/api/v1/W/P/states/` | StateListCreateAPIEndpoint | (APIView) |
| state.py | `/api/v1/W/P/states/<uuid:state_id>/` | StateDetailAPIEndpoint | (APIView) |
| user.py | `/api/v1/users/me/` | UserEndpoint | (APIView) |
| work_item.py | `/api/v1/W/issues/search/` | IssueSearchEndpoint | (APIView) |
| work_item.py | `/api/v1/W/issues/<str:project_identifier>-<str:issue_identifier>/` | WorkspaceIssueAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/issues/` | IssueListCreateAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/issues/<uuid:pk>/` | IssueDetailAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/issues/<uuid:issue_id>/links/` | IssueLinkListCreateAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/issues/<uuid:issue_id>/links/<uuid:pk>/` | IssueLinkDetailAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/issues/<uuid:issue_id>/comments/` | IssueCommentListCreateAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/issues/<uuid:issue_id>/comments/<uuid:pk>/` | IssueCommentDetailAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/issues/<uuid:issue_id>/activities/` | IssueActivityListAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/issues/<uuid:issue_id>/activities/<uuid:pk>/` | IssueActivityDetailAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/issues/<uuid:issue_id>/issue-attachments/` | IssueAttachmentListCreateAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/issues/<uuid:issue_id>/issue-attachments/<uuid:pk>/` | IssueAttachmentDetailAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/work-items/search/` | IssueSearchEndpoint | (APIView) |
| work_item.py | `/api/v1/W/work-items/<str:project_identifier>-<str:issue_identifier>/` | WorkspaceIssueAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/work-items/` | IssueListCreateAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/work-items/<uuid:pk>/` | IssueDetailAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/work-items/<uuid:issue_id>/links/` | IssueLinkListCreateAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/work-items/<uuid:issue_id>/links/<uuid:pk>/` | IssueLinkDetailAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/work-items/<uuid:issue_id>/comments/` | IssueCommentListCreateAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/work-items/<uuid:issue_id>/comments/<uuid:pk>/` | IssueCommentDetailAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/work-items/<uuid:issue_id>/activities/` | IssueActivityListAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/work-items/<uuid:issue_id>/activities/<uuid:pk>/` | IssueActivityDetailAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/work-items/<uuid:issue_id>/attachments/` | IssueAttachmentListCreateAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/work-items/<uuid:issue_id>/attachments/<uuid:pk>/` | IssueAttachmentDetailAPIEndpoint | (APIView) |
| work_item.py | `/api/v1/W/P/work-items/<uuid:issue_id>/relations/` | IssueRelationListCreateAPIEndpoint | (APIView) |

## 公開ボード API（匿名/任意ログイン）

| 定義ファイル | ルート | View | HTTP メソッド → アクション |
|---|---|---|---|
| asset.py | `/api/public/assets/v2/anchor/<str:anchor>/` | EntityAssetEndpoint | (APIView) |
| asset.py | `/api/public/assets/v2/anchor/<str:anchor>/<uuid:pk>/` | EntityAssetEndpoint | (APIView) |
| asset.py | `/api/public/assets/v2/anchor/<str:anchor>/restore/<uuid:pk>/` | AssetRestoreEndpoint | (APIView) |
| asset.py | `/api/public/assets/v2/anchor/<str:anchor>/<uuid:entity_id>/bulk/` | EntityBulkAssetEndpoint | (APIView) |
| intake.py | `/api/public/anchor/<str:anchor>/intakes/<uuid:intake_id>/intake-issues/` | IntakeIssuePublicViewSet | GET:list POST:create |
| intake.py | `/api/public/anchor/<str:anchor>/intakes/<uuid:intake_id>/inbox-issues/` | IntakeIssuePublicViewSet | GET:list POST:create |
| intake.py | `/api/public/anchor/<str:anchor>/intakes/<uuid:intake_id>/intake-issues/<uuid:pk>/` | IntakeIssuePublicViewSet | GET:retrieve PATCH:partial_update DELETE:destroy |
| intake.py | `/api/public/W/project-boards/` | WorkspaceProjectDeployBoardEndpoint | (APIView) |
| issue.py | `/api/public/anchor/<str:anchor>/issues/<uuid:issue_id>/` | IssueRetrievePublicEndpoint | (APIView) |
| issue.py | `/api/public/anchor/<str:anchor>/issues/<uuid:issue_id>/comments/` | IssueCommentPublicViewSet | GET:list POST:create |
| issue.py | `/api/public/anchor/<str:anchor>/issues/<uuid:issue_id>/comments/<uuid:pk>/` | IssueCommentPublicViewSet | GET:retrieve PATCH:partial_update DELETE:destroy |
| issue.py | `/api/public/anchor/<str:anchor>/issues/<uuid:issue_id>/reactions/` | IssueReactionPublicViewSet | GET:list POST:create |
| issue.py | `/api/public/anchor/<str:anchor>/issues/<uuid:issue_id>/reactions/<str:reaction_code>/` | IssueReactionPublicViewSet | DELETE:destroy |
| issue.py | `/api/public/anchor/<str:anchor>/comments/<uuid:comment_id>/reactions/` | CommentReactionPublicViewSet | GET:list POST:create |
| issue.py | `/api/public/anchor/<str:anchor>/comments/<uuid:comment_id>/reactions/<str:reaction_code>/` | CommentReactionPublicViewSet | DELETE:destroy |
| issue.py | `/api/public/anchor/<str:anchor>/issues/<uuid:issue_id>/votes/` | IssueVotePublicViewSet | GET:list POST:create DELETE:destroy |
| project.py | `/api/public/anchor/<str:anchor>/meta/` | ProjectMetaDataEndpoint | (APIView) |
| project.py | `/api/public/anchor/<str:anchor>/settings/` | ProjectDeployBoardPublicSettingsEndpoint | (APIView) |
| project.py | `/api/public/anchor/<str:anchor>/issues/` | ProjectIssuesPublicEndpoint | (APIView) |
| project.py | `/api/public/W/P/anchor/` | WorkspaceProjectAnchorEndpoint | (APIView) |
| project.py | `/api/public/anchor/<str:anchor>/cycles/` | ProjectCyclesEndpoint | (APIView) |
| project.py | `/api/public/anchor/<str:anchor>/modules/` | ProjectModulesEndpoint | (APIView) |
| project.py | `/api/public/anchor/<str:anchor>/states/` | ProjectStatesEndpoint | (APIView) |
| project.py | `/api/public/anchor/<str:anchor>/labels/` | ProjectLabelsEndpoint | (APIView) |
| project.py | `/api/public/anchor/<str:anchor>/members/` | ProjectMembersEndpoint | (APIView) |

## 認証（Django View・フォーム POST + リダイレクト）

| 定義ファイル | ルート | View | HTTP メソッド → アクション |
|---|---|---|---|
| urls.py | `/auth/sign-in/` | SignInAuthEndpoint | (APIView) |
| urls.py | `/auth/sign-up/` | SignUpAuthEndpoint | (APIView) |
| urls.py | `/auth/spaces/sign-in/` | SignInAuthSpaceEndpoint | (APIView) |
| urls.py | `/auth/spaces/sign-up/` | SignUpAuthSpaceEndpoint | (APIView) |
| urls.py | `/auth/sign-out/` | SignOutAuthEndpoint | (APIView) |
| urls.py | `/auth/spaces/sign-out/` | SignOutAuthSpaceEndpoint | (APIView) |
| urls.py | `/auth/get-csrf-token/` | CSRFTokenEndpoint | (APIView) |
| urls.py | `/auth/magic-generate/` | MagicGenerateEndpoint | (APIView) |
| urls.py | `/auth/magic-sign-in/` | MagicSignInEndpoint | (APIView) |
| urls.py | `/auth/magic-sign-up/` | MagicSignUpEndpoint | (APIView) |
| urls.py | `/auth/spaces/magic-generate/` | MagicGenerateSpaceEndpoint | (APIView) |
| urls.py | `/auth/spaces/magic-sign-in/` | MagicSignInSpaceEndpoint | (APIView) |
| urls.py | `/auth/spaces/magic-sign-up/` | MagicSignUpSpaceEndpoint | (APIView) |
| urls.py | `/auth/google/` | GoogleOauthInitiateEndpoint | (APIView) |
| urls.py | `/auth/google/callback/` | GoogleCallbackEndpoint | (APIView) |
| urls.py | `/auth/spaces/google/` | GoogleOauthInitiateSpaceEndpoint | (APIView) |
| urls.py | `/auth/spaces/google/callback/` | GoogleCallbackSpaceEndpoint | (APIView) |
| urls.py | `/auth/github/` | GitHubOauthInitiateEndpoint | (APIView) |
| urls.py | `/auth/github/callback/` | GitHubCallbackEndpoint | (APIView) |
| urls.py | `/auth/spaces/github/` | GitHubOauthInitiateSpaceEndpoint | (APIView) |
| urls.py | `/auth/spaces/github/callback/` | GitHubCallbackSpaceEndpoint | (APIView) |
| urls.py | `/auth/gitlab/` | GitLabOauthInitiateEndpoint | (APIView) |
| urls.py | `/auth/gitlab/callback/` | GitLabCallbackEndpoint | (APIView) |
| urls.py | `/auth/spaces/gitlab/` | GitLabOauthInitiateSpaceEndpoint | (APIView) |
| urls.py | `/auth/spaces/gitlab/callback/` | GitLabCallbackSpaceEndpoint | (APIView) |
| urls.py | `/auth/email-check/` | EmailCheckEndpoint | (APIView) |
| urls.py | `/auth/spaces/email-check/` | EmailCheckSpaceEndpoint | (APIView) |
| urls.py | `/auth/forgot-password/` | ForgotPasswordEndpoint | (APIView) |
| urls.py | `/auth/reset-password/<uidb64>/<token>/` | ResetPasswordEndpoint | (APIView) |
| urls.py | `/auth/spaces/forgot-password/` | ForgotPasswordSpaceEndpoint | (APIView) |
| urls.py | `/auth/spaces/reset-password/<uidb64>/<token>/` | ResetPasswordSpaceEndpoint | (APIView) |
| urls.py | `/auth/change-password/` | ChangePasswordEndpoint | (APIView) |
| urls.py | `/auth/set-password/` | SetUserPasswordEndpoint | (APIView) |
| urls.py | `/auth/gitea/` | GiteaOauthInitiateEndpoint | (APIView) |
| urls.py | `/auth/gitea/callback/` | GiteaCallbackEndpoint | (APIView) |
| urls.py | `/auth/spaces/gitea/` | GiteaOauthInitiateSpaceEndpoint | (APIView) |
| urls.py | `/auth/spaces/gitea/callback/` | GiteaCallbackSpaceEndpoint | (APIView) |

## インスタンス管理（/api/instances/）

`apps/api/plane/license/urls.py` で定義。InstanceEndpoint、InstanceAdmin（サインイン/サインアップ/サインアウト/me）、InstanceConfiguration、メール機能無効化、ワークスペース一覧・作成等。管理画面（apps/admin、パス `/god-mode/`）から利用される。
