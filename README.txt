FlashFixer
==========

برنامج ويندوز بيمسح فلاشة USB ويعملها فورمات (FAT32 تحت 32 جيجا، و exFAT من 32 جيجا وطالع).
مش صفحة ويب. المتصفح مش يقدر يشوف الفلاشات ولا يشغّل diskpart.

مهم: جرّبه الأول على فلاشة قديمة مش مهمة. الهارد الخارجي المتوصل بـ USB هيظهر في القائمة.

التوافق
- من ويندوز 7 SP1 لحد ويندوز 11، 32 بت و 64 بت، ملف EXE واحد
- ويندوز XP مش مدعوم
- .NET Framework 4.8 لازم يكون متسطب (جاهز على ويندوز 10 إصدار 1903 وويندوز 11)
- من غير تثبيت: انسخ FlashFixer.exe وفولدر lang

من الكود لحد ملف EXE — بالخطوات
1) فك ضغط فولدر FlashFixer على الكمبيوتر، مثلاً على سطح المكتب.
2) نزّل Visual Studio Community من موقع مايكروسوفت (مجاني).
   أثناء التثبيت علّم على: .NET desktop development
   ده بيجيب .NET Framework 4.8 معاه.
3) افتح Visual Studio → Open → Folder → اختار فولدر FlashFixer
   (الفولدر اللي فيه FlashFixer.csproj)
4) فوق على اليمين غيّر Debug إلى Release.
5) قائمة Build → Build Solution.
6) لما يكتب Build succeeded، الملف هنا:
   FlashFixer\bin\Release\net48\FlashFixer.exe
   لازم فولدر lang يكون جنبه. البناء بينسخه لوحده.
7) انسخ الفولدر net48 كله لأي مكان وشغّل FlashFixer.exe.
   مش محتاج تثبيت. أول مرة ويندوز هيسأل عن صلاحية المدير وقت الإصلاح بس.

لو عندك .NET SDK ومتسطب Developer Pack 4.8، من داخل الفولدر في CMD:
  dotnet build -c Release

ويندوز XP مش هيشغّل الملف ده. أقل حاجة ويندوز 7 SP1، ويفضّل 10 أو 11.
الشكل (ناري / مائي / سماوي / ديناميكي / أساسي) والليل والنهار بيتسجلوا لوحدهم.


التشغيل
- دبل كليك على FlashFixer.exe
- أو من CMD عادي (من غير أدمن) عشان تشوف القائمة بس:
  FlashFixer.exe --list --lang ar

اللغة
- لوحده بياخد لغة الويندوز
- أو: FlashFixer.exe --lang ar
- اللغات: ar  en  zh-Hans  fr
- الصيني المختصر بيتفتح كمان لو الويندوز باعت zh أو zh-CN

قبل ما تديه لأي حد
1) dotnet build -c Release
2) FlashFixer.exe --list --lang en
   لازم الفلاشة تظهر BusType=7
3) من الواجهة: اختار الفلاشة، علّم المربع، اكتب PhysicalDrive ورقمه بالظبط، وافق على صلاحية المدير
4) في جهاز الكمبيوتر لازم تظهر باسم FLASH

لو فشلت: ابعت سطر CMD كامل وملف
%TEMP%\flashfixer-*.log
