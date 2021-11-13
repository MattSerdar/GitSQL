GitHub:
    ProductHeader
    RepoOwnerName
    RepoName
    RepoPath

Command Args:
    SearchFromDate: /st
    SearchToDate:   /en
        Both the search dates take the form of any valid date field that can be
        converted to a DateTime object

    IgnoredFilePattern: /i
        The list of multiple file patterns that you want to ignore.
        Add a comma between items.
        * matches zero or more characters within a file or directory name
        ? matches any single character within a file or directory name

    DirectorySortOrder: /so
        This setting will determine the order that the output file gets built in. It will
        follow the order of these directories and those not defined will go last.
        Just enter the directory name itself and not the full path

    FileSearchPattern: /s
        Add a pattern to search by that is used when identifying files.
        If empty the default will be all files.
        This does support multiples so add a comma between your search patterns.
        * matches zero or more characters within a file or directory name
        ? matches any single character within a file or directory name

    OutputFileName: /f
        The name of the generated file that contains all the content
        If empty then it will be the name of this exe with a txt extension

    OutputDirectory: /o
        The location for the files being generated
        If empty then it will be the location of this executing exe
        The only special cases are (D)esktop and MD(MyDocuments), otherwise use a full UNC path.
        Note that the system automatically creates a folder within the OutputDirectory with
        the same name as the running exe to help keep files organized in one clean location

Search Settings:
    SubDirectories:
        If you just want specific sub directories that would fall below the
        Directories key above then list them here.
        It can also be left empty to search all.

    FileExtensions:
        The explicit file extensions to look for.
        Defaults to all if empty - note that this may have untested issues arise
        if your collection seems incorrect then look at your Directories key as well


Output Settings:
    LogToConsole:
        Verbose output to the console

    TruncateOutputFiles:
        Setting TruncateOutputFiles to true will clear out the OutputDirectory
        of any prior files that match the current values set for both OutputFileName*
        The only acceptable values are true, false, or empty
        The default will be false if empty



