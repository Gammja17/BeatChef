// 웹빌드 곡 업로드: 브라우저 파일 피커 → IndexedDB 저장 → Web Audio 디코딩 → Unity로 샘플 전달
// 저장: DB 'beatchef' / store 'songs' (key = 곡 이름, value = 원본 파일 ArrayBuffer)
mergeInto(LibraryManager.library, {

  $beatchefDb: function (cb) {
    var req = indexedDB.open('beatchef', 1);
    req.onupgradeneeded = function () { req.result.createObjectStore('songs'); };
    req.onsuccess = function () { cb(req.result); };
    req.onerror = function () { cb(null); }; // 시크릿 모드 등 — 저장 없이 진행
  },

  $beatchefDecode: function (goName, name, buf) {
    var ctx = new (window.AudioContext || window.webkitAudioContext)();
    ctx.decodeAudioData(buf, function (audio) {
      // 모노 믹스다운 — 게임은 분석/재생 모두 모노면 충분
      var len = audio.length;
      var mono = new Float32Array(len);
      for (var c = 0; c < audio.numberOfChannels; c++) {
        var data = audio.getChannelData(c);
        for (var i = 0; i < len; i++) mono[i] += data[i] / audio.numberOfChannels;
      }
      window.beatchefSamples = mono;
      SendMessage(goName, 'OnWebSongDecoded', name + '|' + audio.sampleRate + '|' + len);
      ctx.close();
    }, function (err) {
      SendMessage(goName, 'OnWebSongError', '디코딩 실패: ' + err);
    });
  },

  BeatChef_OpenFilePicker__deps: ['$beatchefDb', '$beatchefDecode'],
  BeatChef_OpenFilePicker: function (goNamePtr) {
    var goName = UTF8ToString(goNamePtr);
    var input = document.getElementById('beatchef-file');
    if (!input) {
      input = document.createElement('input');
      input.type = 'file';
      input.accept = 'audio/*,.mp3,.wav,.ogg';
      input.id = 'beatchef-file';
      input.style.display = 'none';
      document.body.appendChild(input);
    }
    input.onchange = function (e) {
      var file = e.target.files[0];
      if (!file) return;
      var name = file.name.replace(/\.[^.]+$/, '');
      SendMessage(goName, 'OnWebSongDecoding', name);
      file.arrayBuffer().then(function (buf) {
        // decodeAudioData가 버퍼를 비워버리므로 저장용 사본을 먼저 뜬다
        var copy = buf.slice(0);
        beatchefDb(function (db) {
          if (!db) return;
          var tx = db.transaction('songs', 'readwrite');
          tx.objectStore('songs').put(copy, name);
          tx.oncomplete = function () { SendMessage(goName, 'OnWebSongSaved', name); };
        });
        beatchefDecode(goName, name, buf);
      }).catch(function (err) {
        SendMessage(goName, 'OnWebSongError', '파일 읽기 실패: ' + err);
      });
    };
    input.value = '';
    input.click();
  },

  BeatChef_ListSavedSongs__deps: ['$beatchefDb'],
  BeatChef_ListSavedSongs: function (goNamePtr) {
    var goName = UTF8ToString(goNamePtr);
    beatchefDb(function (db) {
      if (!db) { SendMessage(goName, 'OnSavedSongList', ''); return; }
      var req = db.transaction('songs').objectStore('songs').getAllKeys();
      req.onsuccess = function () { SendMessage(goName, 'OnSavedSongList', req.result.join('\n')); };
      req.onerror = function () { SendMessage(goName, 'OnSavedSongList', ''); };
    });
  },

  BeatChef_LoadSavedSong__deps: ['$beatchefDb', '$beatchefDecode'],
  BeatChef_LoadSavedSong: function (goNamePtr, namePtr) {
    var goName = UTF8ToString(goNamePtr);
    var name = UTF8ToString(namePtr);
    SendMessage(goName, 'OnWebSongDecoding', name);
    beatchefDb(function (db) {
      if (!db) { SendMessage(goName, 'OnWebSongError', '브라우저 저장소를 열 수 없어요'); return; }
      var req = db.transaction('songs').objectStore('songs').get(name);
      req.onsuccess = function () {
        if (!req.result) { SendMessage(goName, 'OnWebSongError', '저장된 곡을 찾을 수 없어요'); return; }
        beatchefDecode(goName, name, req.result);
      };
    });
  },

  BeatChef_DeleteSavedSong__deps: ['$beatchefDb'],
  BeatChef_DeleteSavedSong: function (namePtr) {
    var name = UTF8ToString(namePtr);
    beatchefDb(function (db) {
      if (db) db.transaction('songs', 'readwrite').objectStore('songs').delete(name);
    });
  },

  BeatChef_CopySamples: function (ptr, length) {
    if (!window.beatchefSamples) return 0;
    var n = Math.min(length, window.beatchefSamples.length);
    HEAPF32.set(window.beatchefSamples.subarray(0, n), ptr >> 2);
    window.beatchefSamples = null;
    return n;
  }
});
