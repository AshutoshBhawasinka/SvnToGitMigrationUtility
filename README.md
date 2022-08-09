Migrates data from SVN for any given path to a Git Repository maintaining commit message and the timestamp by going back in time and adding files to the git repository.

Copyright: Ashutosh Bhawasinka(2000-2022)

Usage:
SvnToGitHubMigrator.exe $SvnUrl $WorkingDirectory [$StartSvnRevision]

where:

$SvSvnUrl          : The SVN URL from where to migrate the data. Only 
                    files under the provided link is migrated.
					
$WorkingDirectory  : The directory in which this tool will retrieve the content 
                    from SVN in a folder named 'SVN' and also create a git 
                    repository in a folder named 'GIT'). 

$StartSvnRevision  : Specifies the starting svn revision at which to start.
                    Revisions prior to this will be skipped. When not specified,
                    all revisions are migrated

