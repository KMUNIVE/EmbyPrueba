﻿define([], function () {

    var query = {

        StartIndex: 0,
        Limit: 100000
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

    function reloadList(page) {

        Dashboard.showLoadingMsg();

        ApiClient.getSmartMatchInfos(query).then(function (infos) {

            currentResult = infos;

            populateList(page, infos);

            Dashboard.hideLoadingMsg();

        }, function () {

            Dashboard.hideLoadingMsg();
        });
    }

    function populateList(page, result) {

        var infos = result.Items;

        if (infos.length > 0) {
            infos = infos.sort(function (a, b) {

                a = a.OrganizerType + " " + (a.DisplayName || a.ItemName);
                b = b.OrganizerType + " " + (b.DisplayName || b.ItemName);

                if (a == b) {
                    return 0;
                }

                if (a < b) {
                    return -1;
                }

                return 1;
            });
        }

        var html = "";

        if (infos.length) {
            html += '<div class="paperList">';
        }

        for (var i = 0, length = infos.length; i < length; i++) {

            var info = infos[i];

            html += '<paper-icon-item>';

            html += '<paper-fab mini icon="folder" item-icon class="blue"></paper-fab>';

            html += (info.DisplayName || info.ItemName);

            html += '</paper-icon-item>';

            var matchStringIndex = 0;

            html += info.MatchStrings.map(function (m) {

                var matchStringHtml = '';
                matchStringHtml += '<paper-icon-item>';

                matchStringHtml += '<paper-item-body>';

                matchStringHtml += "<div secondary>" + m + "</div>";

                matchStringHtml += '</paper-item-body>';

                matchStringHtml += '<button type="button" is="paper-icon-button-light" class="btnDeleteMatchEntry" data-index="' + i + '" data-matchindex="' + matchStringIndex + '" title="' + Globalize.translate('ButtonDelete') + '"><iron-icon icon="delete"></iron-icon></button>';

                matchStringHtml += '</paper-icon-item>';
                matchStringIndex++;

                return matchStringHtml;

            }).join('');
        }

        if (infos.length) {
            html += "</div>";
        }

        var matchInfos = page.querySelector('.divMatchInfos');
        matchInfos.innerHTML = html;
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

        var self = this;

        var divInfos = view.querySelector('.divMatchInfos');

        divInfos.addEventListener('click', function (e) {

            var button = parentWithClass(e.target, 'btnDeleteMatchEntry');

            if (button) {

                var index = parseInt(button.getAttribute('data-index'));
                var matchIndex = parseInt(button.getAttribute('data-matchindex'));

                var info = currentResult.Items[index];
                var entries = [
                {
                    Name: info.ItemName,
                    Value: info.MatchStrings[matchIndex]
                }];

                ApiClient.deleteSmartMatchEntries(entries).then(function () {

                    reloadList(view);

                }, Dashboard.processErrorResponse);
            }
        });

        view.addEventListener('viewshow', function (e) {

            LibraryMenu.setTabs('autoorganize', 2, getTabs);
            Dashboard.showLoadingMsg();

            reloadList(view);
        });

        view.addEventListener('viewhide', function (e) {

            currentResult = null;
        });
    };
});