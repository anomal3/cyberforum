// Читалка темы. Форум пускает к страницам тем только настоящий браузер, поэтому
// грузим его страницу как есть, а потом выкидываем всё лишнее и пересобираем
// сообщения в свою вёрстку прямо в DOM.
(function () {
  try {
  /* Проверяем не флажок, а саму разметку: страницу могут перерисовать заново,
     и тогда причёсывать надо по новой. */
  if (document.getElementById('cf-reader')) {
    return 'уже причёсано';
  }

  var styles = decodeURIComponent(escape(atob('%%CSS%%')));

  function text(node) {
    return node ? (node.textContent || '').replace(/\s+/g, ' ').trim() : '';
  }

  // блок кода на форуме — таблица GeSHi с колонкой номеров строк
  function rebuildCode(root) {
    var blocks = root.querySelectorAll('div.codeblock, div.printablecode');

    for (var i = 0; i < blocks.length; i++) {
      var block = blocks[i];
      /* в шапке блока рядом с названием языка живут кнопки форума —
         «Скопировано», «Выделить код» и прочее, их в заголовок тащить незачем */
      var language = text(block.querySelector('td.head, .head'))
        .replace(/(скопирован[оа]?|копировать|выделить.*|развернуть.*|свернуть.*)/gi, '')
        .replace(/\s+/g, ' ')
        .trim() || 'код';
      var pre = block.querySelector('td.de1 pre') || block.querySelector('code') || block.querySelector('pre');

      if (!pre) {
        continue;
      }

      var figure = document.createElement('figure');
      figure.className = 'cf-code';

      var bar = document.createElement('div');
      bar.className = 'cf-code-bar';

      var name = document.createElement('span');
      name.textContent = language;

      var copy = document.createElement('button');
      copy.className = 'cf-copy';
      copy.textContent = 'копировать';

      bar.appendChild(name);
      bar.appendChild(copy);

      var holder = document.createElement('pre');
      var code = document.createElement('code');
      code.innerHTML = pre.innerHTML.replace(/ /g, ' ');

      holder.appendChild(code);
      figure.appendChild(bar);
      figure.appendChild(holder);

      copy.addEventListener('click', function (source) {
        return function () {
          var value = source.textContent || '';

          if (navigator.clipboard) {
            navigator.clipboard.writeText(value);
          }

          this.textContent = 'скопировано';
        };
      }(code));

      block.parentNode.replaceChild(figure, block);
    }
  }

  // цитата свёрстана вложенными таблицами, делаем из неё blockquote
  function rebuildQuotes(root) {
    var quotes = root.querySelectorAll('div.bbcode_quote, div.quotebox');

    for (var i = 0; i < quotes.length; i++) {
      var quote = quotes[i];
      var author = text(quote.querySelector('div.bbcode_postedby')) || 'Цитата';
      var body = quote.querySelector('div.message');

      var block = document.createElement('blockquote');
      block.className = 'cf-quote';

      var head = document.createElement('div');
      head.className = 'cf-quote-author';
      head.textContent = author;

      var content = document.createElement('div');
      content.className = 'cf-quote-body';
      content.innerHTML = body ? body.innerHTML : quote.innerHTML;

      block.appendChild(head);
      block.appendChild(content);

      var container = quote.closest('table.bbcode_maincontainer') || quote;
      container.parentNode.replaceChild(block, container);
    }
  }

  /* Внешние ссылки форум помечает target="_blank". WebView на такое отвечает
     попыткой открыть новое окно, а окон у нас нет — палец нажимает, и ничего
     не происходит. Снимаем метку, дальше ссылку перехватит уже приложение. */
  function tidyLinks(root) {
    var links = root.querySelectorAll('a');

    for (var i = 0; i < links.length; i++) {
      links[i].removeAttribute('target');
      links[i].removeAttribute('onclick');
    }
  }

  /* Картинку по тапу показываем своим просмотрщиком, а вложение — качаем сами:
     WebView ни то, ни другое сам не умеет. Сообщаем об этом приложению сменой
     адреса на выдуманную схему, её перехватывает страница. */
  function catchTaps(root) {
    var images = root.querySelectorAll('img.cf-image');

    for (var i = 0; i < images.length; i++) {
      images[i].style.cursor = 'zoom-in';
      images[i].addEventListener('click', function (event) {
        event.preventDefault();
        location.href = 'cfimage:' + encodeURIComponent(this.src);
      });
    }

    var files = root.querySelectorAll('a[href*="attachment.php"], a[href*="/attachments/"]');

    for (var f = 0; f < files.length; f++) {
      files[f].addEventListener('click', function (event) {
        event.preventDefault();
        location.href = 'cffile:' + encodeURIComponent(this.href);
      });
    }
  }

  function tidyImages(root) {
    var images = root.querySelectorAll('img');

    for (var i = 0; i < images.length; i++) {
      var image = images[i];
      var source = image.getAttribute('src') || '';

      image.removeAttribute('width');
      image.removeAttribute('height');
      image.removeAttribute('style');
      image.className = source.indexOf('/smilies/') >= 0 ? 'cf-smiley' : 'cf-image';

      /* битую картинку лучше убрать совсем, чем показывать обломок с alt-текстом */
      image.addEventListener('error', function () {
        this.remove();
      });

      /* некоторые адреса приходят без протокола или по http — на https-странице это не грузится */
      if (source.indexOf('//') === 0) {
        image.src = 'https:' + source;
      } else if (source.indexOf('http://') === 0) {
        image.src = 'https://' + source.substring(7);
      }
    }
  }

  /* Спойлер на форуме раскрывает свой скрипт, а мы его выкидываем вместе со всей
     страницей — тело так и осталось бы спрятанным. Делаем из него обычный details,
     он умеет разворачиваться сам. */
  function rebuildSpoilers(root) {
    var wraps = root.querySelectorAll('div.spoiler-wrap');

    for (var i = 0; i < wraps.length; i++) {
      var wrap = wraps[i];
      var head = wrap.querySelector('.spoiler-head');
      var body = wrap.querySelector('.spoiler-body');

      if (!body) {
        continue;
      }

      var box = document.createElement('details');
      box.className = 'cf-spoiler';

      var title = document.createElement('summary');
      title.textContent = text(head) || 'Спойлер';

      var inside = document.createElement('div');
      inside.innerHTML = body.innerHTML;
      inside.style.display = 'block';

      box.appendChild(title);
      box.appendChild(inside);

      wrap.parentNode.replaceChild(box, wrap);
    }
  }

  /* Форум местами прибивает цвета прямо в разметке — «комментарий модератора»,
     например, идёт светлым по белому. На тёмной теме это нечитаемо, поэтому
     чужие фоны и цвета выкидываем. Подсветку кода не трогаем: она к этому
     времени уже лежит внутри .cf-code и живёт по своим правилам. */
  function tidyColors(root) {
    var all = root.querySelectorAll('[style], [bgcolor], [color], font, table, td');

    for (var i = 0; i < all.length; i++) {
      var node = all[i];

      if (node.closest && node.closest('.cf-code')) {
        continue;
      }

      node.removeAttribute('bgcolor');
      node.removeAttribute('color');
      node.removeAttribute('width');
      node.removeAttribute('height');

      var style = node.getAttribute('style');

      if (style) {
        style = style
          .replace(/(^|;)\s*(background|background-color|color|width|height)\s*:[^;]*/gi, '')
          .replace(/^;+|;+$/g, '');

        if (style.trim()) {
          node.setAttribute('style', style);
        } else {
          node.removeAttribute('style');
        }
      }
    }
  }

  /* Форум зарабатывает вставками от партнёров, и незачем лишать его этого —
     у гостя мы их сохраняем и переносим в собранную страницу, отдельным блоком
     под сообщением. Вошедшим форум их и сам не показывает, так что выкидываем. */
  var keptInserts = [];

  function stripJunk(root, keep) {
    var junk = root.querySelectorAll(
      'iframe, ins, .adsbygoogle, [id^="yandex_rtb"], [class*="adsbygoogle"], ' +
      'a[href*="studwork"], a[href*="click"], img[src*="studwork"], img[alt*="Студворк"]');

    for (var i = 0; i < junk.length; i++) {
      if (keep && junk[i].closest('.cf-insert') === null) {
        var holder = document.createElement('div');
        holder.className = 'cf-insert';
        holder.appendChild(junk[i]);
        keptInserts.push(holder);
        continue;
      }

      junk[i].remove();
    }
  }

  /* Страницу прячем с первого же захода и до тех пор, пока не соберём свою:
     сырая вёрстка форума не должна мелькать вообще. Сам WebView прозрачный,
     так что человек в это время видит нашу заставку под ним. */
  function hidePage() {
    if (document.getElementById('cf-hide')) {
      return;
    }

    var root = document.head || document.documentElement;

    if (!root) {
      return;
    }

    var sheet = document.createElement('style');
    sheet.id = 'cf-hide';
    sheet.textContent = 'html,body{visibility:hidden!important;background:transparent!important}';
    root.appendChild(sheet);
  }

  function showPage() {
    var sheet = document.getElementById('cf-hide');

    if (sheet) {
      sheet.remove();
    }
  }

  /* Форму быстрого ответа мы сейчас снесём вместе со всей страницей, а в ней лежат
     одноразовые posthash и poststarttime — без них форум ответ не примет. Забираем
     их себе заранее: тогда отвечать можно, ни за чем больше не ходя на сайт. */
  function saveForm() {
    var form = document.getElementById('qrform');

    // заодно запоминаем, узнал ли нас форум: без этого ни ответить, ни поблагодарить
    window.cfSigned = !!document.querySelector('a[href*="do=logout"]');

    if (!form) {
      window.cfForm = null;
      return;
    }

    var fields = {};
    var inputs = form.querySelectorAll('input[name]');

    for (var i = 0; i < inputs.length; i++) {
      var input = inputs[i];
      var type = (input.getAttribute('type') || 'text').toLowerCase();

      if (type === 'submit' || type === 'button' || type === 'file') {
        continue;
      }

      if ((type === 'checkbox' || type === 'radio') && !input.checked) {
        continue;
      }

      fields[input.getAttribute('name')] = input.value;
    }

    var attach = document.querySelector('a[href*="newattachment.php"]');

    window.cfForm = {
      action: form.getAttribute('action'),
      fields: fields,
      attach: attach ? attach.getAttribute('href') : null
    };
  }

  hidePage();

  /* Форма лежит в самом низу страницы, а сообщения появляются гораздо раньше —
     причесав тему сразу, мы бы снесли разметку до того, как форма доехала, и
     отвечать стало бы нечем. Поэтому чуть-чуть ждём: секунды хватает, дольше
     ждать незачем — у гостя формы не будет вовсе. */
  /* Ждём не саму форму, а её последнее поле: одноразовый posthash дописывается
     в самом низу, и форма, снятая раньше, приезжает пустой оболочкой. */
  if (!document.querySelector('#qrform input[name="posthash"]')) {
    window.cfWait = (window.cfWait || 0) + 1;

    if (window.cfWait < 12 && document.readyState !== 'complete') {
      return 'ждём форму';
    }
  }

  saveForm();

  var messages = document.querySelectorAll('div[id^="post_message_"]');

  if (!messages.length) {
    return 'ждём сообщения';
  }

  var posts = [];

  for (var i = 0; i < messages.length; i++) {
    var message = messages[i];
    var id = message.id.replace('post_message_', '');
    var container = document.getElementById('post' + id) || message.parentNode;

    /* Обычно имя лежит в a.bigusername, но у только что отправленного сообщения
       форум отдаёт шапку попроще. Тогда ищем ссылку на профиль сами и пропускаем
       те, где вместо имени число: рядом такой же ссылкой висит репутация. */
    // у своего свежего сообщения имя приходит не ссылкой, а простым span
    var authorLink = container.querySelector('.bigusername');

    if (!authorLink) {
      var maybe = container.querySelectorAll('a[href*="members/"]');

      for (var m = 0; m < maybe.length; m++) {
        var label = text(maybe[m]);

        if (label && !/^[\d+\-\s]+$/.test(label)) {
          authorLink = maybe[m];
          break;
        }
      }
    }
    var author = text(authorLink);
    var authorHref = authorLink ? (authorLink.getAttribute('href') || '') : '';
    var authorId = (authorHref.match(/members\/(\d+)\.html/) || [])[1] || '';
    var avatar = container.querySelector('img[src*="customavatars"]');
    var when = '';

    var cells = container.querySelectorAll('td.smallfont, div.smallfont');
    for (var c = 0; c < cells.length; c++) {
      var found = text(cells[c]).match(/(\d{2}\.\d{2}\.\d{4}|Сегодня|Вчера),?\s*\d{1,2}:\d{2}/);
      if (found) {
        when = found[0];
        break;
      }
    }

    var anchor = container.querySelector('a[href*="#post' + id + '"]');

    posts.push({
      id: id,
      author: author.replace(/^@/, ''),
      authorId: authorId,
      avatar: avatar ? avatar.getAttribute('src') : null,
      when: when,
      number: text(anchor),
      best: !!container.querySelector('img[src*="tick.png"]'),
      html: message.innerHTML
    });
  }

  var title = document.querySelector('h1.content');
  var titleText = text(title) || document.title.split(' - ')[0];

  /* Отвечать может только вошедший, ему форум и рисует форму быстрого ответа.
     Автору темы вдобавок доступна отметка лучшего ответа — он же её и раздаёт.

     Сравниваем по имени, а не по номеру: у своих сообщений форум показывает имя
     простым текстом, без ссылки на профиль, и номера там взять негде. */
  var canPost = !!window.cfForm;
  var guest = !window.cfSigned;
  var me = '%%ME%%';
  var threadStarter = posts.length ? posts[0].author : '';

  function icon(path) {
    return '<svg viewBox="0 0 24 24" aria-hidden="true">' + path + '</svg>';
  }

  var iconReply = icon('<path d="M10 8V4l-7 7 7 7v-4c5 0 8.5 1.6 11 5-1-5-4-10-11-11z"/>');
  var iconThanks = icon('<path d="M6 21H3V10h3zm3-11 3.6-5.4c.4-.6 1.2-.7 1.8-.3.4.3.6.8.5 1.3L14 10h5.5c1 0 1.8 1 1.6 2l-1.4 6.5c-.2.9-1 1.5-1.9 1.5H9z"/>');
  var iconBest = icon('<path d="M12 3l6 3v5c0 4-2.6 7.4-6 8.5C8.6 18.4 6 15 6 11V6z"/><path d="M9.5 11.5l1.8 1.8 3.4-3.4"/>');

  /* Кнопки в самом сообщении, а не в общей шапке: палец уже здесь, и понятно,
     к какому именно сообщению относится действие. Приложение ловит нажатие
     по смене адреса на выдуманную схему. */
  function actionBar(post) {
    var bar = document.createElement('div');
    bar.className = 'cf-acts';

    function add(kind, label, drawing, shown) {
      if (!shown) {
        return;
      }

      var button = document.createElement('button');
      button.className = 'cf-act';
      button.setAttribute('data-kind', kind);
      button.innerHTML = drawing + '<span>' + label + '</span>';

      button.addEventListener('click', function () {
        location.href = 'cfact:' + kind + ':' + post.id + ':' + encodeURIComponent(post.author);
      });

      bar.appendChild(button);
    }

    var mine = me !== '' && post.author === me;

    add('quote', 'Ответить', iconReply, true);
    add('thank', 'Спасибо', iconThanks, me !== '' && !mine);
    add('best', 'Лучший ответ', iconBest, me !== '' && me === threadStarter && !mine);

    return bar;
  }

  // собираем свою страницу с нуля: так гарантированно уходят шапка, подвал и вставки
  var page = document.createElement('div');
  page.id = 'cf-reader';

  var heading = document.createElement('h1');
  heading.className = 'cf-title';
  heading.textContent = titleText;
  page.appendChild(heading);

  for (var p = 0; p < posts.length; p++) {
    var post = posts[p];

    var article = document.createElement('article');
    article.className = 'cf-post' + (post.best ? ' best' : '');
    article.id = 'post-' + post.id;

    var head = document.createElement('div');
    head.className = 'cf-head';

    if (post.avatar) {
      var picture = document.createElement('img');
      picture.className = 'cf-avatar';
      picture.src = post.avatar;
      head.appendChild(picture);
    } else {
      var stub = document.createElement('div');
      stub.className = 'cf-avatar';
      head.appendChild(stub);
    }

    var who = document.createElement('div');
    who.className = 'cf-who';

    var name = document.createElement('span');
    name.className = 'cf-author';
    name.textContent = post.author;

    var date = document.createElement('span');
    date.className = 'cf-when';
    date.textContent = post.when;

    who.appendChild(name);
    who.appendChild(date);
    head.appendChild(who);

    if (post.number) {
      var number = document.createElement('span');
      number.className = 'cf-num';
      number.textContent = '#' + post.number;
      head.appendChild(number);
    }

    article.appendChild(head);

    if (post.best) {
      var badge = document.createElement('div');
      badge.className = 'cf-badge';
      badge.textContent = 'Лучший ответ';
      article.appendChild(badge);
    }

    var body = document.createElement('div');
    body.className = 'cf-body';
    body.innerHTML = post.html;

    stripJunk(body, guest);
    rebuildSpoilers(body);
    rebuildCode(body);
    rebuildQuotes(body);
    tidyColors(body);
    tidyLinks(body);
    tidyImages(body);
    catchTaps(body);

    article.appendChild(body);

    if (canPost) {
      article.appendChild(actionBar(post));
    }

    page.appendChild(article);

    if (guest && keptInserts.length > 0 && (p === 0 || p === posts.length - 1)) {
      page.appendChild(keptInserts.shift());
    }
  }

  // что не поместилось между сообщениями — ставим внизу
  while (guest && keptInserts.length > 0) {
    page.appendChild(keptInserts.shift());
  }

  /* Листалку собираем по всем ссылкам вида thread123-page7.html, а не по тем,
     где подписью стоит число: в шапке форума половина страниц спрятана
     за «Последняя» и многоточием, и так мы теряли хвост длинных тем. */
  var known = {};
  var last = 1;
  var pattern = '';
  var links = document.querySelectorAll('a[href*="-page"]');

  for (var l = 0; l < links.length; l++) {
    var href = links[l].getAttribute('href') || '';
    var match = href.match(/(thread\d+)-page(\d+)\.html/);

    if (!match) {
      continue;
    }

    var number = parseInt(match[2], 10);
    known[number] = href;

    if (number > last) {
      last = number;
      pattern = href.replace(/-page\d+\.html/, '');
    }
  }

  var here = 1;
  var mine = location.href.match(/thread\d+-page(\d+)\.html/);

  if (mine) {
    here = parseInt(mine[1], 10);
  }

  if (last > 1) {
    var nav = document.createElement('nav');
    nav.className = 'cf-pages';

    function pageLink(number, label, weak) {
      if (number < 1 || number > last) {
        return;
      }

      var href = known[number] || (pattern + '-page' + number + '.html');

      var link = document.createElement('a');
      link.href = href;
      link.textContent = label;

      if (number === here) {
        link.className = 'here';
      } else if (weak) {
        link.className = 'weak';
      }

      nav.appendChild(link);
    }

    pageLink(here - 1, '‹ назад', true);

    // рядом с текущей показываем соседей, чтобы не листать вслепую
    var from = Math.max(1, here - 2);
    var to = Math.min(last, here + 2);

    if (from > 1) {
      pageLink(1, '1', true);
    }

    for (var n = from; n <= to; n++) {
      pageLink(n, String(n), false);
    }

    if (to < last) {
      pageLink(last, String(last), true);
    }

    pageLink(here + 1, 'вперёд ›', true);

    page.appendChild(nav);
  }

  if (!document.head || !document.body) {
    return 'ждём разметку';
  }

  document.head.innerHTML = '';
  document.body.innerHTML = '';

  var sheet = document.createElement('style');
  sheet.textContent = styles;
  document.head.appendChild(sheet);

  var viewport = document.createElement('meta');
  viewport.name = 'viewport';
  viewport.content = 'width=device-width, initial-scale=1, viewport-fit=cover';
  document.head.appendChild(viewport);

  document.body.appendChild(page);

  /* В длинной теме мотать пальцем от начала к концу — занятие на минуту,
     поэтому сбоку живёт кнопка-прыгалка: сверху зовёт вниз, снизу — обратно. */
  var jump = document.createElement('button');
  jump.className = 'cf-jump';
  jump.textContent = '↓';
  jump.setAttribute('aria-label', 'в конец темы');

  jump.addEventListener('click', function () {
    var down = jump.textContent === '↓';
    window.scrollTo({ top: down ? document.body.scrollHeight : 0, behavior: 'smooth' });
  });

  window.addEventListener('scroll', function () {
    var bottom = window.scrollY + window.innerHeight > document.body.scrollHeight - 200;
    jump.textContent = bottom ? '↑' : '↓';
    jump.style.opacity = document.body.scrollHeight > window.innerHeight * 1.8 ? '1' : '0';
  });

  if (document.body.scrollHeight > window.innerHeight * 1.8) {
    document.body.appendChild(jump);
  }

  showPage();

  // Чужие скрипты живут своей жизнью и продолжают лезть в страницу уже после
  // того, как мы её пересобрали. Сторож выкидывает всё, что не наше.
  var guard = new MutationObserver(function (records) {
    for (var r = 0; r < records.length; r++) {
      var added = records[r].addedNodes;

      for (var a = 0; a < added.length; a++) {
        var node = added[a];

        if (node.nodeType !== 1) {
          continue;
        }

        if (node.id === 'cf-reader' || node.className === 'cf-jump' ||
            node.tagName === 'STYLE' || node.tagName === 'META') {
          continue;
        }

        /* Гостю партнёрские вставки оставляем: их скрипты дорисовывают себя уже
           после нашей сборки, и сторож не должен считать это чужим мусором. */
        if (guest && (node.tagName === 'INS' || node.tagName === 'IFRAME' ||
            (node.id || '').indexOf('yandex_rtb') === 0)) {
          continue;
        }

        if (node.closest && node.closest('#cf-reader')) {
          continue;
        }

        node.remove();
      }
    }
  });

  guard.observe(document.documentElement, { childList: true, subtree: true });

  return 'постов: ' + posts.length + ', форма: ' + (canPost ? 'есть' : 'нет');
  } catch (error) {
    return 'ошибка: ' + (error && error.message ? error.message : error);
  }
})();
