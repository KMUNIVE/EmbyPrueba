﻿define(['jQuery', 'datetime', 'paper-icon-button-light', 'paper-spinner'], function ($, datetime) {

    var query = {

        StartIndex: 0,
        Limit: 50
    };

    var currentResult;

    function parentWithClass(elem, className) {

        while (!elem.classList || !elem.classList.contains(className)) {
            elem = elem.parentNode;

            if (!elem) {
                return null;
            }
        }

        return elem;
    }

    function showStatusMessage(id) {

        var item = currentResult.Items.filter(function (i) {

            return i.Id == id;
        })[0];

        Dashboard.alert({

            title: getStatusText(item, false),
            message: item.StatusMessage
        });
    }

    function deleteOriginalFile(page, id) {

        var item = currentResult.Items.filter(function (i) {

            return i.Id == id;
        })[0];

        var message = Globalize.translate('MessageFileWillBeDeleted') + '<br/><br/>' + item.OriginalPath + '<br/><br/>' + Globalize.translate('MessageSureYouWishToProceed');

        require(['confirm'], function (confirm) {

            confirm(message, Globalize.translate('HeaderDeleteFile')).then(function () {

                Dashboard.showLoadingMsg();

                ApiClient.deleteOriginalFileFromOrganizationResult(id).then(function () {

                    Dashboard.hideLoadingMsg();

                    reloadItems(page, true);

                }, Dashboard.processErrorResponse);
            });
        });
    }

    function organizeFileWithCorrections(page, item) {

        showCorrectionPopup(page, item);
    }

    function showCorrectionPopup(page, item) {

        require(['components/fileorganizer/fileorganizer'], function (fileorganizer) {

            fileorganizer.show(item).then(function () {
                reloadItems(page, false);
            });
        });
    }

    function organizeFile(page, id) {

        var item = currentResult.Items.filter(function (i) {

            return i.Id == id;
        })[0];

        if (!item.TargetPath) {

            if (item.Type == "Episode") {
                organizeFileWithCorrections(page, item);
            }

            return;
        }

        var message = Globalize.translate('MessageFollowingFileWillBeMovedFrom') + '<br/><br/>' + item.OriginalPath + '<br/><br/>' + Globalize.translate('MessageDestinationTo') + '<br/><br/>' + item.TargetPath;

        if (item.DuplicatePaths.length) {
            message += '<br/><br/>' + Globalize.translate('MessageDuplicatesWillBeDeleted');

            message += '<br/><br/>' + item.DuplicatePaths.join('<br/>');
        }

        message += '<br/><br/>' + Globalize.translate('MessageSureYouWishToProceed');

        require(['confirm'], function (confirm) {

            confirm(message, Globalize.translate('HeaderOrganizeFile')).then(function () {

                Dashboard.showLoadingMsg();

                ApiClient.performOrganization(id).then(function () {

                    Dashboard.hideLoadingMsg();

                    reloadItems(page, true);

                }, Dashboard.processErrorResponse);
            });
        });
    }

    function reloadItems(page, showSpinner) {

        if (showSpinner) {
            Dashboard.showLoadingMsg();
        }

        ApiClient.getFileOrganizationResults(query).then(function (result) {

            currentResult = result;
            renderResults(page, result);

            Dashboard.hideLoadingMsg();
        }, Dashboard.processErrorResponse);
    }

    function getStatusText(item, enhance) {

        var status = item.Status;

        var color = null;

        if (status == 'SkippedExisting') {
            status = Globalize.translate('StatusSkipped');
        }
        else if (status == 'Failure') {
            color = '#cc0000';
            status = Globalize.translate('StatusFailed');
        }
        if (status == 'Success') {
            color = 'green';
            status = Globalize.translate('StatusSuccess');
        }

        if (enhance) {

            if (item.StatusMessage) {

                return '<a style="color:' + color + ';" data-resultid="' + item.Id + '" href="#" class="btnShowStatusMessage">' + status + '</a>';
            } else {
                return '<span data-resultid="' + item.Id + '" style="color:' + color + ';">' + status + '</span>';
            }
        }

        return status;
    }

    function renderResults(page, result) {

        var rows = result.Items.map(function (item) {

            var html = '';

            html += '<tr>';

            html += '<td class="spinnerCell">';
            var spinnerActive = item.IsInProgress ? 'active' : '';
            html += '<paper-spinner class="syncSpinner"' + spinnerActive + ' style="vertical-align: middle; /">';
            html += '</td>';

            html += '<td data-title="Date">';

            var date = datetime.parseISO8601Date(item.Date, true);
            html += date.toLocaleDateString();

            html += '</td>';

            html += '<td data-title="Source" class="fileCell">';
            var status = item.Status;

            if (item.IsInProgress) {
                html += '<div style="color:darkorange;">';
                html += item.OriginalFileName;
                html += '</div>';
            }
            else if (status == 'SkippedExisting') {
                html += '<a data-resultid="' + item.Id + '" style="color:blue;" href="#" class="btnShowStatusMessage">';
                html += item.OriginalFileName;
                html += '</a>';
            }
            else if (status == 'Failure') {
                html += '<a data-resultid="' + item.Id + '" style="color:red;" href="#" class="btnShowStatusMessage">';
                html += item.OriginalFileName;
                html += '</a>';
            } else {
                html += '<div style="color:green;">';
                html += item.OriginalFileName;
                html += '</div>';
            }
            html += '</td>';

            html += '<td data-title="Destination" class="fileCell">';
            html += item.TargetPath || '';
            html += '</td>';

            html += '<td class="organizerButtonCell">';

            if (item.Status != 'Success') {

                html += '<button type="button" is="paper-icon-button-light" data-resultid="' + item.Id + '" class="btnProcessResult organizerButton" title="' + Globalize.translate('ButtonOrganizeFile') + '"><iron-icon icon="folder"></iron-icon></button>';
                html += '<button type="button" is="paper-icon-button-light" data-resultid="' + item.Id + '" class="btnDeleteResult organizerButton" title="' + Globalize.translate('ButtonDeleteFile') + '"><iron-icon icon="delete"></iron-icon></button>';
            }

            html += '</td>';

            html += '</tr>';

            return html;
        }).join('');

        var resultBody = page.querySelector('.resultBody');
        resultBody.innerHTML = rows;

        resultBody.addEventListener('click', handleItemClick);

        var pagingHtml = LibraryBrowser.getQueryPagingHtml({
            startIndex: query.StartIndex,
            limit: query.Limit,
            totalRecordCount: result.TotalRecordCount,
            showLimit: false,
            updatePageSizeSetting: false
        });

        var topPaging = page.querySelector('.listTopPaging');
        topPaging.innerHTML = pagingHtml;

        var bottomPaging = page.querySelector('.listBottomPaging');
        bottomPaging.innerHTML = pagingHtml;

        var btnNextTop = topPaging.querySelector(".btnNextPage");
        var btnNextBottom = bottomPaging.querySelector(".btnNextPage");
        var btnPrevTop = topPaging.querySelector(".btnPreviousPage");
        var btnPrevBottom = bottomPaging.querySelector(".btnPreviousPage");

        btnNextTop.addEventListener('click', function () {
            query.StartIndex += query.Limit;
            reloadItems(page, true);
        });

        btnNextBottom.addEventListener('click', function () {
            query.StartIndex += query.Limit;
            reloadItems(page, true);
        });

        btnPrevTop.addEventListener('click', function () {
            query.StartIndex -= query.Limit;
            reloadItems(page, true);
        });

        btnPrevBottom.addEventListener('click', function () {
            query.StartIndex -= query.Limit;
            reloadItems(page, true);
        });

        var btnClearLog = page.querySelector('.btnClearLog');

        if (result.TotalRecordCount) {
            btnClearLog.classList.remove('hide');
        } else {
            btnClearLog.classList.add('hide');
        }
    }

    function handleItemClick(e) {

        var buttonStatus = parentWithClass(e.target, 'btnShowStatusMessage');
        if (buttonStatus) {

            var id = buttonStatus.getAttribute('data-resultid');
            showStatusMessage(id);
        }

        var buttonOrganize = parentWithClass(e.target, 'btnProcessResult');
        if (buttonOrganize) {

            var id = buttonOrganize.getAttribute('data-resultid');
            organizeFile(e.view, id);
        }

        var buttonDelete = parentWithClass(e.target, 'btnDeleteResult');
        if (buttonDelete) {

            var id = buttonDelete.getAttribute('data-resultid');
            deleteOriginalFile(e.view, id);
        }
    }

    function getTabs() {
        return [
        {
            href: 'autoorganizelog.html',
            name: Globalize.translate('TabActivityLog')
        },
         {
             href: 'autoorganizetv.html',
             name: Globalize.translate('TabTV')
         },
         {
             href: 'autoorganizesmart.html',
             name: Globalize.translate('TabSmartMatches')
         }];
    }


    return function (view, params) {

        function onWebSocketMessage(e, msg) {

            if ((msg.MessageType == 'ScheduledTaskEnded' && msg.Data.Key == 'AutoOrganize') || msg.MessageType == 'AutoOrganizeUpdate') {

                reloadItems(view, false);
            }
        }

        var clearButton = view.querySelector('.btnClearLog');
        clearButton.addEventListener('click', function () {

            ApiClient.clearOrganizationLog().then(function () {
                reloadItems(view, true);
            }, Dashboard.processErrorResponse);
        });

        view.addEventListener('viewshow', function (e) {

            LibraryMenu.setTabs('autoorganize', 0, getTabs);
            Dashboard.showLoadingMsg();

            reloadItems(view, true);

            //var organizeButton = view.querySelector('.btnOrganize');

            $('.btnOrganize', view).taskButton({
                mode: 'on',
                progressElem: view.querySelector('.organizeProgress'),
                panel: view.querySelector('.organizeTaskPanel'),
                taskKey: 'AutoOrganize'
            });

            Events.on(ApiClient, 'websocketmessage', onWebSocketMessage);
        });

        view.addEventListener('viewhide', function (e) {

            currentResult = null;

            //var organizeButton = view.querySelector('.btnOrganize');

            $('.btnOrganize', page).taskButton({
                mode: 'off'
            });

            Events.off(ApiClient, 'websocketmessage', onWebSocketMessage);
        });
    };
});