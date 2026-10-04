# Overview of build decisions
## Data Structure
I decided that with the structure of the orders and customers that a Postgresql databasxe would be a good quick solution that can then leverage off of the structured data system to keep those relations all structured well and be able to buidl some async calls when making additions and edits so that error processing could be processed as well.
I also made some decisions on things like the SKU being an optional but unique field if present, as sometimes from experience there is not always a value to be used for this but we also don't want items in the same order that do have them present to be doubled up on.
I also made teh decision that the unique thing of each Customer was their email, needing that to be unique for each as that is a pretty standard practice and their might be a shared phone number or multiple people with the same name across multiple organisations.
This also let me build some basic migrations to make sure that the solution can be run as needed and if I wanted to that I could then containerise the solution to have a basic setup that could be run where I needed

## Validation
### Front End
The front end was quite secondary to the solution due to the nature of most of the supplied business rules being required to be processed on the back end, but that did not mean that it was not useful to add in some extra checks for when deleting an order or customer. I made the decision to add in a state for when stock had been procured bt not confirmed to be sent out and that with the basic flow meant that I made a couple of decisions on which options could be used from the front end. I also tried to make the front end have a bit more meaningful error messages.

### Back End
Many of the validations for the additions of items were made on the back end and informed by the rules provided. This could by and large be enforced by picking the right data types on the database that is recording the values but it was good to have these in place on the API calls so that if someone were to try access it they could get good info. A consideration would be to enable swagger for these end points in a future update.

### Time Out
The other big thing that needed to be handled here though is the time out of adding duplicate entries. I made sure that this is a configurable time limit from the start and worked with the suggested solution to create a unique string for the entries based on the contents of the item being added and then comparing if that string matches an item and if that item's created is within the time out to block new entries.

This allows for slightly quicker responses from the check as we don't need to check through all entries but just query if there is a duplicate of that unique string, and only if we find it then do the comparison of the created time. There was some thought also into making sure that an order that has the same line items but in a different order was still flagged as being a duplicate as well.

## Flow
I made the decision that all newly created orders need to be started in the `Pending` state, to be picked up for processing, looking to see if those items are available or not. At any stage an order can be moved to `Cancelled` but otherwise there is a one direction flow to the eventual `Completed` state. This felt liek a good balance for an initial run, though I would expect there to be needs for more states when customers make requests during processing or if there would need to be items added on to the orders as well later in the process. In which case the sditing would need to go back to the processing states, but as a first go of things I don't think that consideration was within the bounds of the solution.