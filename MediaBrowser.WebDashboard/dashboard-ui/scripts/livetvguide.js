﻿(function ($, document) {

    // 30 mins
    var cellCurationMinutes = 30;
    var cellDurationMs = cellCurationMinutes * 60 * 1000;
    var msPerDay = 86400000;

    var currentDate;

    var defaultChannels = 50;
    var channelLimit = 1000;

    var channelQuery = {

        StartIndex: 0,
        Limit: defaultChannels,
        EnableFavoriteSorting: true
    };

    var channelsPromise;

    function normalizeDateToTimeslot(date) {

        var minutesOffset = date.getMinutes() - cellCurationMinutes;

        if (minutesOffset >= 0) {

            date.setHours(date.getHours(), cellCurationMinutes, 0, 0);

        } else {

            date.setHours(date.getHours(), 0, 0, 0);
        }

        return date;
    }

    function reloadChannels(page) {
        channelsPromise = null;
        reloadGuide(page);
    }

    function reloadGuide(page) {

        Dashboard.showModalLoadingMsg();

        channelQuery.UserId = Dashboard.getCurrentUserId();

        channelQuery.Limit = Math.min(channelQuery.Limit || defaultChannels, channelLimit);
        channelQuery.AddCurrentProgram = false;

        channelsPromise = channelsPromise || ApiClient.getLiveTvChannels(channelQuery);

        var date = currentDate;

        var nextDay = new Date(date.getTime() + msPerDay - 1);
        Logger.log(nextDay);
        channelsPromise.done(function (channelsResult) {

            ApiClient.getLiveTvPrograms({
                UserId: Dashboard.getCurrentUserId(),
                MaxStartDate: nextDay.toISOString(),
                MinEndDate: date.toISOString(),
                channelIds: channelsResult.Items.map(function (c) {
                    return c.Id;
                }).join(','),
                ImageTypeLimit: 1,
                EnableImageTypes: "Primary"

            }).done(function (programsResult) {

                renderGuide(page, date, channelsResult.Items, programsResult.Items);

                Dashboard.hideModalLoadingMsg();

                LibraryBrowser.setLastRefreshed(page);

            });

            var channelPagingHtml = LibraryBrowser.getQueryPagingHtml({
                startIndex: channelQuery.StartIndex,
                limit: channelQuery.Limit,
                totalRecordCount: channelsResult.TotalRecordCount,
                updatePageSizeSetting: false,
                showLimit: true
            });

            var channelPaging = page.querySelector('.channelPaging');
            channelPaging.innerHTML = channelPagingHtml;
            $(channelPaging).trigger('create');

            Events.on(page.querySelector('.btnNextPage'), 'click', function () {
                channelQuery.StartIndex += channelQuery.Limit;
                reloadChannels(page);
            });

            Events.on(page.querySelector('.btnPreviousPage'), 'click', function () {
                channelQuery.StartIndex -= channelQuery.Limit;
                reloadChannels(page);
            });

            Events.on(page.querySelector('#selectPageSize'), 'change', function () {
                channelQuery.Limit = parseInt(this.value);
                channelQuery.StartIndex = 0;
                reloadChannels(page);
            });
        });
    }

    function getTimeslotHeadersHtml(startDate, endDateTime) {

        var html = '';

        // clone
        startDate = new Date(startDate.getTime());

        html += '<div class="timeslotHeadersInner">';

        while (startDate.getTime() < endDateTime) {

            html += '<div class="timeslotHeader">';
            html += '<div class="timeslotHeaderInner">';

            html += LibraryBrowser.getDisplayTime(startDate);
            html += '</div>';
            html += '</div>';

            // Add 30 mins
            startDate.setTime(startDate.getTime() + cellDurationMs);
        }
        html += '</div>';

        return html;
    }

    function parseDates(program) {

        if (!program.StartDateLocal) {
            try {

                program.StartDateLocal = parseISO8601Date(program.StartDate, { toLocal: true });

            } catch (err) {

            }

        }

        if (!program.EndDateLocal) {
            try {

                program.EndDateLocal = parseISO8601Date(program.EndDate, { toLocal: true });

            } catch (err) {

            }

        }

        return null;
    }

    function getChannelProgramsHtml(page, date, channel, programs) {

        var html = '';

        var startMs = date.getTime();
        var endMs = startMs + msPerDay - 1;

        programs = programs.filter(function (curr) {
            return curr.ChannelId == channel.Id;
        });

        html += '<div class="channelPrograms">';

        for (var i = 0, length = programs.length; i < length; i++) {

            var program = programs[i];

            if (program.ChannelId != channel.Id) {
                continue;
            }

            parseDates(program);

            if (program.EndDateLocal.getTime() < startMs) {
                continue;
            }

            if (program.StartDateLocal.getTime() > endMs) {
                break;
            }

            var renderStartMs = Math.max(program.StartDateLocal.getTime(), startMs);
            var startPercent = (program.StartDateLocal.getTime() - startMs) / msPerDay;
            startPercent *= 100;
            startPercent = Math.max(startPercent, 0);

            var renderEndMs = Math.min(program.EndDateLocal.getTime(), endMs);
            var endPercent = (renderEndMs - renderStartMs) / msPerDay;
            endPercent *= 100;

            html += '<div class="programCell" style="left:' + startPercent + '%;width:' + endPercent + '%;">';

            var cssClass = "programCellInner";
            var addAccent = true;

            if (program.IsKids) {
                cssClass += " childProgramInfo";
            } else if (program.IsSports) {
                cssClass += " sportsProgramInfo";
            } else if (program.IsNews) {
                cssClass += " newsProgramInfo";
            } else if (program.IsMovie) {
                cssClass += " movieProgramInfo";
            }
            else {
                cssClass += " plainProgramInfo";
                addAccent = false;
            }

            html += '<a href="itemdetails.html?id=' + program.Id + '" class="' + cssClass + '" data-programid="' + program.Id + '">';

            html += '<div class="guideProgramName">';
            html += program.Name;
            html += '</div>';

            html += '<div class="guideProgramTime">';
            if (program.IsLive) {
                html += '<span class="liveTvProgram">' + Globalize.translate('LabelLiveProgram') + '&nbsp;&nbsp;</span>';
            }
            else if (program.IsPremiere) {
                html += '<span class="premiereTvProgram">' + Globalize.translate('LabelPremiereProgram') + '&nbsp;&nbsp;</span>';
            }
            else if (program.IsSeries && !program.IsRepeat) {
                html += '<span class="newTvProgram">' + Globalize.translate('LabelNewProgram') + '&nbsp;&nbsp;</span>';
            }

            html += LibraryBrowser.getDisplayTime(program.StartDateLocal);
            html += ' - ';
            html += LibraryBrowser.getDisplayTime(program.EndDateLocal);

            if (program.SeriesTimerId) {
                html += '<div class="timerCircle seriesTimerCircle"></div>';
                html += '<div class="timerCircle seriesTimerCircle"></div>';
                html += '<div class="timerCircle seriesTimerCircle"></div>';
            }
            else if (program.TimerId) {

                html += '<div class="timerCircle"></div>';
            }
            html += '</div>';

            if (addAccent) {
                html += '<div class="programAccent"></div>';
            }

            html += '</a>';

            html += '</div>';
        }

        html += '</div>';

        return html;
    }

    function renderPrograms(page, date, channels, programs) {

        var html = [];

        for (var i = 0, length = channels.length; i < length; i++) {

            html.push(getChannelProgramsHtml(page, date, channels[i], programs));
        }

        var programGrid = page.querySelector('.programGrid');
        programGrid.innerHTML = html.join('');

        $(programGrid).scrollTop(0).scrollLeft(0)
            .createGuideHoverMenu('.programCellInner');
    }

    function renderChannelHeaders(page, channels) {

        var html = '';

        for (var i = 0, length = channels.length; i < length; i++) {

            var channel = channels[i];

            html += '<div class="channelHeaderCellContainer">';

            html += '<div class="channelHeaderCell">';
            html += '<a class="channelHeaderCellInner" href="itemdetails.html?id=' + channel.Id + '">';

            var hasChannelImage = channel.ImageTags.Primary;
            var cssClass = hasChannelImage ? 'guideChannelInfo guideChannelInfoWithImage' : 'guideChannelInfo';

            html += '<div class="' + cssClass + '">' + channel.Name + '<br/>' + channel.Number + '</div>';

            if (hasChannelImage) {

                var url = ApiClient.getScaledImageUrl(channel.Id, {
                    maxHeight: 35,
                    maxWidth: 60,
                    tag: channel.ImageTags.Primary,
                    type: "Primary"
                });

                html += '<img class="guideChannelImage" src="' + url + '" />';
            }

            html += '</a>';
            html += '</div>';

            html += '</div>';
        }

        page.querySelector('.channelList').innerHTML = html;
    }

    function renderGuide(page, date, channels, programs) {

        renderChannelHeaders(page, channels);

        var startDate = date;
        var endDate = new Date(startDate.getTime() + msPerDay);
        page.querySelector('.timeslotHeaders').innerHTML = getTimeslotHeadersHtml(startDate, endDate);
        renderPrograms(page, date, channels, programs);
    }

    var gridScrolling = false;
    var headersScrolling = false;
    function onProgramGridScroll(page, elem) {

        if (!headersScrolling) {
            gridScrolling = true;

            $(page.querySelector('.timeslotHeaders')).scrollLeft($(elem).scrollLeft());
            gridScrolling = false;
        }
    }

    function onTimeslotHeadersScroll(page, elem) {

        if (!gridScrolling) {
            headersScrolling = true;
            $(page.querySelector('.programGrid')).scrollLeft($(elem).scrollLeft());
            headersScrolling = false;
        }
    }

    function changeDate(page, date) {

        currentDate = normalizeDateToTimeslot(date);

        reloadGuide(page);

        var text = LibraryBrowser.getFutureDateText(date);
        text = '<span class="currentDay">' + text.replace(' ', ' </span>');
        page.querySelector('.currentDate').innerHTML = text;
    }

    var dateOptions = [];

    function setDateRange(page, guideInfo) {

        var today = new Date();
        today.setHours(today.getHours(), 0, 0, 0);

        var start = parseISO8601Date(guideInfo.StartDate, { toLocal: true });
        var end = parseISO8601Date(guideInfo.EndDate, { toLocal: true });

        start.setHours(0, 0, 0, 0);
        end.setHours(0, 0, 0, 0);

        if (start.getTime() >= end.getTime()) {
            end.setDate(start.getDate() + 1);
        }

        start = new Date(Math.max(today, start));

        dateOptions = [];

        while (start <= end) {

            dateOptions.push({
                name: LibraryBrowser.getFutureDateText(start),
                id: start.getTime(),
                ironIcon: 'today'
            });

            start.setDate(start.getDate() + 1);
            start.setHours(0, 0, 0, 0);
        }

        var date = new Date();

        if (currentDate) {
            date.setTime(currentDate.getTime());
        }

        changeDate(page, date);
    }

    function reloadPageAfterValidation(page, limit) {

        channelLimit = limit;

        ApiClient.getLiveTvGuideInfo().done(function (guideInfo) {

            setDateRange(page, guideInfo);
        });
    }

    function reloadPage(page) {

        $('.guideRequiresUnlock', page).hide();

        RegistrationServices.validateFeature('livetv').done(function () {
            Dashboard.showModalLoadingMsg();

            reloadPageAfterValidation(page, 1000);
        }).fail(function () {

            Dashboard.showModalLoadingMsg();

            var limit = 5;
            $('.guideRequiresUnlock', page).show();
            $('.unlockText', page).html(Globalize.translate('MessageLiveTvGuideRequiresUnlock', limit));

            reloadPageAfterValidation(page, limit);
        });
    }

    function selectDate(page) {

        require(['actionsheet'], function () {

            ActionSheetElement.show({
                items: dateOptions,
                showCancel: true,
                title: Globalize.translate('HeaderSelectDate'),
                callback: function (id) {

                    var date = new Date();
                    date.setTime(parseInt(id));
                    changeDate(page, date);
                }
            });

        });
    }

    window.LiveTvPage.initGuideTab = function (page, tabContent) {

        Events.on(tabContent.querySelector('.programGrid'), 'scroll', function () {

            onProgramGridScroll(tabContent, this);
        });

        if ($.browser.mobile) {
            tabContent.querySelector('.tvGuide').classList.add('mobileGuide');
        } else {

            tabContent.querySelector('.tvGuide').classList.remove('mobileGuide');

            Events.on(tabContent.querySelector('.timeslotHeaders'), 'scroll', function () {

                onTimeslotHeadersScroll(tabContent, this);
            });
        }

        if (AppInfo.enableHeadRoom) {
            requirejs(["thirdparty/headroom"], function () {

                // construct an instance of Headroom, passing the element
                var headroom = new Headroom(tabContent.querySelector('.tvGuideHeader'));
                // initialise
                headroom.init();
            });
        }

        $('.btnUnlockGuide', tabContent).on('click', function () {

            reloadPage(tabContent);
        });

        $('.btnSelectDate', tabContent).on('click', function () {

            selectDate(tabContent);
        });
    };

    window.LiveTvPage.renderGuideTab = function (page, tabContent) {

        if (LibraryBrowser.needsRefresh(tabContent)) {
            reloadPage(tabContent);
        }
    };

})(jQuery, document);