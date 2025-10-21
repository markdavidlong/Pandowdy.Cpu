# Debugger Notes

* I think that there is already a good hook in the AppleSoft Basic interpreter in ROM that allows for printing the line number when ```TRACE``` is enabled (0xD805).  I can use a regular 6502 engine and AppleSoft ROM from a 2e, for instance, and trap that address call.  It will tell me when each line is going to be processed.
* Likewise, I can identify the hooks for creating variables, running garbage collection, and so on, and keep track of what is being added to memory
* This will also handle all of the AppleSoft-specific Machine Language code that is happening behind the scenes and storing values in the zero page.
* I will have to trap IO calls and C000 soft switches, but that's not too difficult. 